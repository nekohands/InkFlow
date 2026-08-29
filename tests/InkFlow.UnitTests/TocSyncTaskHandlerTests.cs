using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

/// <summary>
/// Toc 任务处理器编排验证:目录同步 + 正典映射成功后自动联动正文抓取入队,
/// 映射前置条件不满足时不联动。全部内存执行,零真实网络流量。
/// </summary>
[TestClass]
public sealed class TocSyncTaskHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 11, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Successful_Toc_Sync_Chains_Content_Fetch_Tasks()
    {
        var harness = await CreateHarnessAsync();
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "example-source",
                SourceCapability.Toc,
                new Dictionary<string, string> { ["bookId"] = "10001" }),
            createdAt: T0);

        var outcome = await harness.Handler.ExecuteAsync(task);

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        CollectionAssert.AreEqual(
            new[] { "c1", "c2" },
            harness.Tasks.Store.Select(t => t.Payload.Variables["chapterId"]).ToArray());
        foreach (var chained in harness.Tasks.Store)
        {
            Assert.AreEqual(SourceCapability.Content, chained.Payload.Capability);
            Assert.AreEqual("10001", chained.Payload.Variables["bookId"]);
        }
    }

    [TestMethod]
    public async Task Repeated_Sync_Does_Not_Duplicate_Chained_Tasks()
    {
        var harness = await CreateHarnessAsync();
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "example-source",
                SourceCapability.Toc,
                new Dictionary<string, string> { ["bookId"] = "10001" }),
            createdAt: T0);

        await harness.Handler.ExecuteAsync(task);
        // 第二次目录同步(幂等)时,首轮正文任务仍在途——不得重复入队。
        await harness.Handler.ExecuteAsync(task);

        Assert.AreEqual(2, harness.Tasks.Store.Count);
    }

    [TestMethod]
    public async Task Missing_Confirmed_Match_Fails_Without_Chaining()
    {
        var harness = await CreateHarnessAsync(withConfirmedMatch: false);
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "example-source",
                SourceCapability.Toc,
                new Dictionary<string, string> { ["bookId"] = "10001" }),
            createdAt: T0);

        var outcome = await harness.Handler.ExecuteAsync(task);

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "no confirmed canonical match");
        Assert.AreEqual(0, harness.Tasks.Store.Count);
    }

    [TestMethod]
    public async Task Credential_Reference_Flows_Through_Toc_And_Chained_Content_Tasks()
    {
        var harness = await CreateHarnessAsync();
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "example-source",
                SourceCapability.Toc,
                new Dictionary<string, string> { ["bookId"] = "10001" },
                "source-credential"),
            createdAt: T0);

        var outcome = await harness.Handler.ExecuteAsync(task);

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.AreEqual("source-credential", harness.Adapter.LastExecutionContext!.CredentialReferenceId);
        Assert.IsTrue(harness.Tasks.Store.All(
            chained => chained.Payload.CredentialReferenceId == "source-credential"));
    }

    private static async Task<Harness> CreateHarnessAsync(bool withConfirmedMatch = true)
    {
        var sourceBooks = new InMemorySourceBooks();
        var adapter = new FakeAdapter();
        var catalog = new SourceCatalogService(
            new FixedAdapterFactory(adapter), sourceBooks, TimeProvider.System);

        // 目录同步的前置条件:BookInfo 必须已导入。
        var imported = await catalog.ImportBookInfoAsync("example-source", "10001");
        Assert.IsTrue(imported.IsSuccess, string.Join("; ", imported.Errors));

        var canonical = CanonicalBook.Create("剑来", "烽火戏诸侯", T0);
        var candidates = new InMemoryCandidates();
        if (withConfirmedMatch)
        {
            candidates.Store.Add(MatchCandidate.Confirm(canonical.Id, "example-source", "10001", T0));
        }

        var mappingService = new CanonicalChapterMappingService(
            sourceBooks, candidates, new SingleCanonicalRepository(canonical), new InMemoryMappings());
        var tasks = new ConflictAwareTaskRepository();
        var chain = new ContentFetchChainService(
            sourceBooks, new InMemoryArtifacts(), tasks, TimeProvider.System);

        return new Harness(new TocSyncTaskHandler(catalog, mappingService, chain), tasks, adapter);
    }

    private sealed record Harness(
        TocSyncTaskHandler Handler,
        ConflictAwareTaskRepository Tasks,
        FakeAdapter Adapter);

    /// <summary>与 EfCrawlerTaskRepository 冲突语义一致的内存实现(阻止态含死信)。</summary>
    private sealed class ConflictAwareTaskRepository : ICrawlerTaskRepository
    {
        private static readonly CrawlerTaskStatus[] BlockingStatuses =
        [
            CrawlerTaskStatus.Pending,
            CrawlerTaskStatus.Leased,
            CrawlerTaskStatus.Running,
            CrawlerTaskStatus.DeadLettered,
        ];

        public List<CrawlerTask> Store { get; } = [];

        public Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default)
        {
            Store.Add(task);
            return Task.CompletedTask;
        }

        public Task<CrawlerTask?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(Store.SingleOrDefault(t => t.Id == id));

        public Task<CrawlerTask?> TryLeaseAsync(
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < Store.Count; i++)
            {
                if (Store[i].Id == task.Id)
                {
                    Store[i] = task;
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CrawlerTask>>([]);

        public Task AddDeadLetterAsync(DeadLetterTask deadLetter, CancellationToken cancellationToken = default) =>
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
            Task.FromResult(Store.Any(t =>
                t.Payload.SourceId == sourceId &&
                t.Payload.Capability == capability &&
                BlockingStatuses.Contains(t.Status) &&
                t.Payload.Variables.TryGetValue(variableName, out var value) &&
                value == variableValue));
    }

    private sealed class FixedAdapterFactory(ISourceAdapter? adapter) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(string sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(adapter is not null && adapter.SourceId == sourceId ? adapter : null);
    }

    private sealed class FakeAdapter : ISourceAdapter
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
            Task.FromResult<SourceBookInfo?>(new SourceBookInfo("剑来", "烽火戏诸侯"));

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceTocEntry>>(
            [
                new("c1", 0, "第一章"),
                new("c2", 1, "第二章"),
            ]);

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId,
            CancellationToken cancellationToken,
            SourceExecutionContext? executionContext)
        {
            LastExecutionContext = executionContext;
            return GetTableOfContentsAsync(externalBookId, cancellationToken);
        }

        public Task<string?> GetChapterContentAsync(
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class InMemorySourceBooks : ISourceBookRepository
    {
        public Dictionary<(string SourceId, string ExternalId), SourceBook> Store { get; } = [];

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.TryGetValue((sourceId, externalBookId), out var book) ? book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceBook>>(Store.Values.ToList());

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCandidates : IMatchCandidateRepository
    {
        public List<MatchCandidate> Store { get; } = [];

        public Task AddAsync(MatchCandidate candidate, CancellationToken cancellationToken = default)
        {
            Store.Add(candidate);
            return Task.CompletedTask;
        }

        public Task<MatchCandidate?> FindForSourceBookAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MatchCandidate?>(
                Store.FirstOrDefault(c =>
                    c.SourceId == sourceId && c.ExternalBookId == externalBookId));
    }

    private sealed class SingleCanonicalRepository(CanonicalBook book) : ICanonicalBookRepository
    {
        public Task AddAsync(CanonicalBook b, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(book is not null && book.Id == id ? book : null);

        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CanonicalBook>>([book]);

        public Task<CanonicalBook?> FindByTitleAuthorAsync(
            string title,
            string author,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CanonicalBook?>(null);

        public Task SaveAsync(CanonicalBook b, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryMappings : IChapterMappingRepository
    {
        public List<ChapterMapping> Store { get; } = [];

        public Task AddAsync(ChapterMapping mapping, CancellationToken cancellationToken = default)
        {
            Store.Add(mapping);
            return Task.CompletedTask;
        }

        public Task<ChapterMapping?> FindAsync(
            string sourceId,
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ChapterMapping?>(
                Store.FirstOrDefault(m =>
                    m.SourceId == sourceId && m.ExternalChapterId == externalChapterId));
    }

    private sealed class InMemoryArtifacts : IFetchArtifactRepository
    {
        public Task AddAsync(FetchArtifact artifact, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<FetchArtifact?> GetLatestAsync(
            string sourceId,
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<FetchArtifact?>(null);

        public Task<IReadOnlySet<string>> ListFetchedExternalChapterIdsAsync(
            string sourceId,
            IEnumerable<string> externalChapterIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));

        public Task<IReadOnlySet<string>> ListRecentlyFetchedExternalChapterIdsAsync(
            string sourceId,
            IEnumerable<string> externalChapterIds,
            DateTimeOffset since,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
    }
}
