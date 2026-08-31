using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

/// <summary>
/// BookInfo 子任务编排验证：来源书目导入、正典匹配和 Toc 子任务创建必须保持同一 RunId，
/// 并在导入/匹配失败或运行已进入停止状态时安全收敛。
/// </summary>
[TestClass]
public sealed class BookInfoSyncTaskHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Successful_BookInfo_Sync_Persists_Match_And_Queues_Toc()
    {
        var harness = CreateHarness();
        var task = CreateTask(harness.Run.Id, credentialReferenceId: "platform-book-info");

        var outcome = await harness.Handler.ExecuteAsync(task);

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.IsNotNull(harness.SourceBooks.Get("example-source", "10001"));
        Assert.AreEqual("platform-book-info", harness.Adapter.LastExecutionContext!.CredentialReferenceId);
        Assert.AreEqual(SourceCredentialOwnerKind.Platform,
            harness.Adapter.LastExecutionContext.EffectiveCredentialOwnerScope.Kind);
        Assert.AreEqual(harness.Canonical.Id, harness.Run.CanonicalBookId);
        Assert.AreEqual(CollectionRunStage.Toc, harness.Run.Stage);

        Assert.AreEqual(1, harness.Tasks.Store.Count);
        var tocTask = harness.Tasks.Store[0];
        Assert.AreEqual(SourceCapability.Toc, tocTask.Payload.Capability);
        Assert.AreEqual(harness.Run.Id, tocTask.Payload.RunId);
        Assert.AreEqual("10001", tocTask.Payload.Variables["bookId"]);
        Assert.AreEqual("collection-run", tocTask.Payload.Variables["reason"]);
        Assert.AreEqual("platform-book-info", tocTask.Payload.CredentialReferenceId);
    }

    [TestMethod]
    public async Task Missing_Book_Id_Fails_Before_Source_Call()
    {
        var harness = CreateHarness();
        var task = CrawlerTask.Create(
            new CrawlPayload("example-source", SourceCapability.BookInfo, new Dictionary<string, string>()),
            createdAt: T0);

        var outcome = await harness.Handler.ExecuteAsync(task);

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "missing the required 'bookId' variable");
        Assert.IsNull(harness.Adapter.LastExecutionContext);
        Assert.AreEqual(0, harness.Tasks.Store.Count);
    }

    [TestMethod]
    public async Task Source_Import_Failure_Does_Not_Match_Or_Queue_Toc()
    {
        var harness = CreateHarness(bookInfoAvailable: false);
        var task = CreateTask(harness.Run.Id);

        var outcome = await harness.Handler.ExecuteAsync(task);

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "book 'example-source/10001' was not found");
        Assert.IsNull(harness.SourceBooks.Get("example-source", "10001"));
        Assert.AreEqual(0, harness.Matches.Store.Count);
        Assert.AreEqual(0, harness.Tasks.Store.Count);
        Assert.IsNull(harness.Run.CanonicalBookId);
        Assert.AreEqual(CollectionRunStage.BookInfo, harness.Run.Stage);
    }

    [TestMethod]
    public async Task Dangling_Confirmed_Match_Fails_Without_Queueing_Follow_Up()
    {
        var missingCanonicalId = Guid.NewGuid();
        var harness = CreateHarness(
            candidate: MatchCandidate.Confirm(
                missingCanonicalId, "example-source", "10001", T0));
        var task = CreateTask(harness.Run.Id);

        var outcome = await harness.Handler.ExecuteAsync(task);

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "points to missing book");
        Assert.IsNotNull(harness.SourceBooks.Get("example-source", "10001"));
        Assert.AreEqual(0, harness.Tasks.Store.Count);
        Assert.IsNull(harness.Run.CanonicalBookId);
        Assert.AreEqual(CollectionRunStage.BookInfo, harness.Run.Stage);
    }

    [TestMethod]
    public async Task Stopping_Run_Keeps_Imported_Facts_But_Does_Not_Create_Toc_Task()
    {
        var harness = CreateHarness();
        harness.Run.RequestStop(T0.AddMinutes(1));
        await harness.Runs.SaveAsync(harness.Run);
        var task = CreateTask(harness.Run.Id);

        var outcome = await harness.Handler.ExecuteAsync(task);

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.IsNotNull(harness.SourceBooks.Get("example-source", "10001"));
        Assert.AreEqual(1, harness.Matches.Store.Count);
        Assert.AreEqual(0, harness.Tasks.Store.Count);
        Assert.AreEqual(CollectionRunStatus.Stopping, harness.Run.Status);
        Assert.IsNull(harness.Run.CanonicalBookId);
        Assert.AreEqual(CollectionRunStage.BookInfo, harness.Run.Stage);
    }

    private static CrawlerTask CreateTask(Guid runId, string? credentialReferenceId = null) =>
        CrawlerTask.Create(
            new CrawlPayload(
                "example-source",
                SourceCapability.BookInfo,
                new Dictionary<string, string> { ["bookId"] = "10001" },
                credentialReferenceId,
                runId),
            createdAt: T0);

    private static Harness CreateHarness(
        SourceBookInfo? bookInfo = null,
        MatchCandidate? candidate = null,
        bool bookInfoAvailable = true)
    {
        var source = Source.Create(
            "example-source",
            "Example Source",
            "https://books.example.com",
            T0);
        var sources = new InMemorySourceRepository(source);
        var sourceBooks = new InMemorySourceBookRepository();
        var adapter = new FakeAdapter(
            bookInfoAvailable ? bookInfo ?? new SourceBookInfo("示例书", "示例作者") : null);
        var adapterFactory = new FixedAdapterFactory(adapter);
        var catalog = new SourceCatalogService(
            adapterFactory,
            sourceBooks,
            new FixedTimeProvider(T0));

        var canonical = CanonicalBook.Create("示例书", "示例作者", T0);
        var canonicals = new InMemoryCanonicalBookRepository(canonical);
        var matches = new InMemoryMatchCandidateRepository(candidate);
        var matching = new CanonicalBookMatchingService(sourceBooks, canonicals, matches);
        var tasks = new RecordingTaskRepository();
        var run = CollectionRun.Create(
            "example-source", "10001", "https://books.example.com/book/10001", T0);
        var runs = new InMemoryCollectionRunRepository(run);
        var resolver = new SourceBookUrlResolver(sources, adapterFactory);
        var collectionRuns = new CollectionRunService(
            resolver,
            runs,
            new FixedTimeProvider(T0));
        var handler = new BookInfoSyncTaskHandler(
            catalog,
            matching,
            tasks,
            new FixedTimeProvider(T0),
            collectionRuns);

        return new(handler, adapter, sourceBooks, matches, tasks, runs, run, canonical);
    }

    private sealed record Harness(
        BookInfoSyncTaskHandler Handler,
        FakeAdapter Adapter,
        InMemorySourceBookRepository SourceBooks,
        InMemoryMatchCandidateRepository Matches,
        RecordingTaskRepository Tasks,
        InMemoryCollectionRunRepository Runs,
        CollectionRun Run,
        CanonicalBook Canonical);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedAdapterFactory(ISourceAdapter adapter) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ISourceAdapter?>(adapter.SourceId == sourceId ? adapter : null);
    }

    private sealed class FakeAdapter(SourceBookInfo? bookInfo) : ISourceAdapter
    {
        public string SourceId => "example-source";
        public SourceExecutionContext? LastExecutionContext { get; private set; }

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
            string keyword,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceSearchResult>>([]);

        public Task<SourceBookInfo?> GetBookInfoAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(bookInfo);

        public Task<SourceBookInfo?> GetBookInfoAsync(
            string externalBookId,
            CancellationToken cancellationToken,
            SourceExecutionContext? executionContext)
        {
            LastExecutionContext = executionContext;
            return Task.FromResult(bookInfo);
        }

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceTocEntry>>([]);

        public Task<string?> GetChapterContentAsync(
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public bool TryResolveBookUrl(Uri url, out string externalBookId)
        {
            externalBookId = "10001";
            return true;
        }
    }

    private sealed class InMemorySourceRepository(Source source) : ISourceRepository
    {
        public Task AddAsync(Source value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Source?>(source.Id == sourceId ? source : null);

        public Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Source>>([source]);

        public Task SaveAsync(Source value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemorySourceBookRepository : ISourceBookRepository
    {
        private readonly Dictionary<(string SourceId, string ExternalBookId), SourceBook> _store = [];

        public SourceBook? Get(string sourceId, string externalBookId) =>
            _store.TryGetValue((sourceId, externalBookId), out var book) ? book : null;

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            _store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Get(sourceId, externalBookId));

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceBook>>(_store.Values.ToList());

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            _store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCanonicalBookRepository(CanonicalBook initial) : ICanonicalBookRepository
    {
        private readonly List<CanonicalBook> _store = [initial];

        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            _store.Add(book);
            return Task.CompletedTask;
        }

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.SingleOrDefault(book => book.Id == id));

        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CanonicalBook>>(_store.ToList());

        public Task<CanonicalBook?> FindByTitleAuthorAsync(
            string title,
            string author,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.SingleOrDefault(book => book.Title == title && book.Author == author));

        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemoryMatchCandidateRepository(MatchCandidate? initial) : IMatchCandidateRepository
    {
        public List<MatchCandidate> Store { get; } = initial is null ? [] : [initial];

        public Task AddAsync(MatchCandidate candidate, CancellationToken cancellationToken = default)
        {
            Store.Add(candidate);
            return Task.CompletedTask;
        }

        public Task<MatchCandidate?> FindForSourceBookAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.FirstOrDefault(candidate =>
                candidate.SourceId == sourceId && candidate.ExternalBookId == externalBookId));
    }

    private sealed class RecordingTaskRepository : ICrawlerTaskRepository
    {
        public List<CrawlerTask> Store { get; } = [];

        public Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default)
        {
            Store.Add(task);
            return Task.CompletedTask;
        }

        public Task<CrawlerTask?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(Store.SingleOrDefault(task => task.Id == id));

        public Task<CrawlerTask?> TryLeaseAsync(
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task<CrawlerTask?> TryLeaseAsync(
            Guid taskId,
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CrawlerTask>>([]);

        public Task AddDeadLetterAsync(
            DeadLetterTask deadLetter,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeadLetterTask>>([]);

        public Task<bool> HasActiveTaskAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasConflictingTaskAsync(
            string sourceId,
            SourceCapability capability,
            string variableName,
            string variableValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.Any(task =>
                task.Payload.SourceId == sourceId &&
                task.Payload.Capability == capability &&
                (task.Status is CrawlerTaskStatus.Pending
                    or CrawlerTaskStatus.Leased
                    or CrawlerTaskStatus.Running
                    or CrawlerTaskStatus.DeadLettered) &&
                task.Payload.Variables.TryGetValue(variableName, out var value) &&
                value == variableValue));
    }

    private sealed class InMemoryCollectionRunRepository(CollectionRun initial) : ICollectionRunRepository
    {
        private readonly Dictionary<Guid, CollectionRun> _store = new()
        {
            [initial.Id] = initial,
        };

        public Task AddAsync(CollectionRun run, CancellationToken cancellationToken = default)
        {
            _store[run.Id] = run;
            return Task.CompletedTask;
        }

        public Task<bool> TryAddAsync(CollectionRun run, CancellationToken cancellationToken = default)
        {
            if (_store.ContainsKey(run.Id))
            {
                return Task.FromResult(false);
            }

            _store[run.Id] = run;
            return Task.FromResult(true);
        }

        public Task<bool> TryAddWithInitialTaskAsync(
            CollectionRun run,
            CrawlerTask task,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(task);
            if (task.Payload.RunId != run.Id)
            {
                throw new ArgumentException(
                    "the initial crawler task must reference the collection run.",
                    nameof(task));
            }

            if (_store.Values.Any(candidate =>
                    candidate.SourceId == run.SourceId &&
                    candidate.ExternalBookId == run.ExternalBookId &&
                    candidate.CanScheduleFollowUp))
            {
                return Task.FromResult(false);
            }

            _store[run.Id] = run;
            return Task.FromResult(true);
        }

        public Task<CollectionRun?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(id, out var run) ? run : null);

        public Task<CollectionRun?> FindActiveAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.Values.FirstOrDefault(run =>
                run.SourceId == sourceId &&
                run.ExternalBookId == externalBookId &&
                run.CanScheduleFollowUp));

        public Task<IReadOnlyList<CollectionRun>> ListAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CollectionRun>>(_store.Values.Take(limit).ToList());

        public Task SaveAsync(CollectionRun run, CancellationToken cancellationToken = default)
        {
            _store[run.Id] = run;
            return Task.CompletedTask;
        }

        public Task<CollectionRunTaskProgress> GetTaskProgressAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CollectionRunTaskProgress(0, 0, 0, 0, 0, 0, 0));
    }
}
