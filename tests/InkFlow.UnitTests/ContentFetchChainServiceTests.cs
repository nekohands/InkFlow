using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ContentFetchChainServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Chapters_Without_Artifacts_Are_Enqueued_In_Toc_Order()
    {
        var book = CreateBook(("c1", "第一章"), ("c2", "第二章"), ("c3", "第三章"));
        var harness = CreateHarness(book);

        var enqueued = await harness.Service.EnqueuePendingContentFetchesAsync("example-source", "10001");

        Assert.AreEqual(3, enqueued);
        Assert.AreEqual(3, harness.Tasks.Store.Count);
        CollectionAssert.AreEqual(
            new[] { "c1", "c2", "c3" },
            harness.Tasks.Store.Select(t => t.Payload.Variables["chapterId"]).ToArray());

        foreach (var task in harness.Tasks.Store)
        {
            Assert.AreEqual(SourceCapability.Content, task.Payload.Capability);
            Assert.AreEqual("example-source", task.Payload.SourceId);
            Assert.AreEqual(CrawlerTaskStatus.Pending, task.Status);
            Assert.AreEqual("10001", task.Payload.Variables["bookId"]);
            Assert.AreEqual("new", task.Payload.Variables["reason"], "零产物章节是首次抓取");
        }
    }

    [TestMethod]
    public async Task Stale_Chapters_Are_Refetched_While_Fresh_Ones_Skip()
    {
        var book = CreateBook(("c1", "第一章"), ("c2", "第二章"), ("c3", "第三章"));
        // 保鲜期 6 天:c1 的产物已 7 天(c2 产物只有 1 天,c3 从未抓取)。
        var harness = CreateHarness(book, staleAfter: TimeSpan.FromDays(6));
        Fetch(harness, "c1", fetchedAt: T0.AddDays(-7));
        Fetch(harness, "c2", fetchedAt: T0.AddDays(-1));

        var enqueued = await harness.Service.EnqueuePendingContentFetchesAsync("example-source", "10001");

        Assert.AreEqual(2, enqueued);
        var refetch = harness.Tasks.Store.Single(t => t.Payload.Variables["chapterId"] == "c1");
        Assert.AreEqual("refetch", refetch.Payload.Variables["reason"], "过期章节按修订重扫入队");
        var fresh = harness.Tasks.Store.Where(t => t.Payload.Variables["chapterId"] == "c2").ToList();
        Assert.AreEqual(0, fresh.Count, "保鲜期内的章节不得重抓");
        var firstFetch = harness.Tasks.Store.Single(t => t.Payload.Variables["chapterId"] == "c3");
        Assert.AreEqual("new", firstFetch.Payload.Variables["reason"]);
    }

    [TestMethod]
    public async Task Already_Fetched_Chapters_Are_Skipped()
    {
        var book = CreateBook(("c1", "第一章"), ("c2", "第二章"));
        var harness = CreateHarness(book);
        Fetch(harness, "c1");
        Fetch(harness, "c2");

        var enqueued = await harness.Service.EnqueuePendingContentFetchesAsync("example-source", "10001");

        Assert.AreEqual(0, enqueued);
        Assert.AreEqual(0, harness.Tasks.Store.Count);
    }

    [TestMethod]
    public async Task Partially_Fetched_Book_Enqueues_Only_New_Chapters()
    {
        var book = CreateBook(("c1", "第一章"), ("c2", "第二章"), ("c3", "第三章"));
        var harness = CreateHarness(book);
        Fetch(harness, "c1");

        var enqueued = await harness.Service.EnqueuePendingContentFetchesAsync("example-source", "10001");

        Assert.AreEqual(2, enqueued);
        Assert.IsFalse(
            harness.Tasks.Store.Any(t => t.Payload.Variables["chapterId"] == "c1"),
            "已有产物的章节不得重复入队");
    }

    [TestMethod]
    public async Task Conflicting_Task_Prevents_Duplicate_Enqueue()
    {
        var book = CreateBook(("c1", "第一章"), ("c2", "第二章"));
        var harness = CreateHarness(book);
        harness.Conflicts.Add("Content:chapterId=c2");

        await harness.Service.EnqueuePendingContentFetchesAsync("example-source", "10001");

        Assert.AreEqual(1, harness.Tasks.Store.Count);
        Assert.AreEqual("c1", harness.Tasks.Store.Single().Payload.Variables["chapterId"]);
    }

    [TestMethod]
    public async Task Unavailable_Content_Capability_Disables_Chaining()
    {
        var book = CreateBook(("c1", "第一章"));
        var harness = CreateHarness(book, unavailableSources: ["example-source"]);

        var enqueued = await harness.Service.EnqueuePendingContentFetchesAsync("example-source", "10001");

        Assert.AreEqual(0, enqueued);
        Assert.AreEqual(0, harness.Tasks.Store.Count);
    }

    [TestMethod]
    public async Task Unknown_Book_Returns_Zero()
    {
        var harness = CreateHarness(CreateBook());

        var enqueued = await harness.Service.EnqueuePendingContentFetchesAsync("ghost-source", "999");

        Assert.AreEqual(0, enqueued);
    }

    [TestMethod]
    public async Task Book_Without_Chapters_Returns_Zero()
    {
        var harness = CreateHarness(CreateBook());

        var enqueued = await harness.Service.EnqueuePendingContentFetchesAsync("example-source", "10001");

        Assert.AreEqual(0, enqueued);
    }

    private static SourceBook CreateBook(params (string Id, string Title)[] chapters)
    {
        var book = SourceBook.Create("example-source", "10001", "剑来", "烽火戏诸侯", T0);
        if (chapters.Length > 0)
        {
            book.SyncChapters(chapters, T0);
        }

        return book;
    }

    private static void Fetch(Harness harness, string externalChapterId) =>
        harness.Artifacts.Store.Add(new FetchArtifact(
            Guid.NewGuid(), "example-source", "10001", externalChapterId,
            $"hash-{externalChapterId}", 100, T0));

    private static void Fetch(Harness harness, string externalChapterId, DateTimeOffset fetchedAt) =>
        harness.Artifacts.Store.Add(new FetchArtifact(
            Guid.NewGuid(), "example-source", "10001", externalChapterId,
            $"hash-{externalChapterId}", 100, fetchedAt));

    private static Harness CreateHarness(
        SourceBook book,
        string[]? unavailableSources = null,
        TimeSpan? staleAfter = null)
    {
        var artifacts = new InMemoryArtifacts();
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        var tasks = new RecordingTaskRepository(conflicts);
        var service = new ContentFetchChainService(
            new SingleBookRepository(book),
            artifacts,
            tasks,
            new FixedClock(T0),
            unavailableSources is null ? null : new FixedHealthReader(unavailableSources),
            staleAfter);

        return new Harness(service, tasks, artifacts, conflicts);
    }

    private sealed record Harness(
        ContentFetchChainService Service,
        RecordingTaskRepository Tasks,
        InMemoryArtifacts Artifacts,
        HashSet<string> Conflicts);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedHealthReader(string[] unavailable) : ISourceHealthReader
    {
        public Task<bool> IsAvailableAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(!unavailable.Contains(sourceId));
    }

    private sealed class SingleBookRepository(SourceBook? book) : ISourceBookRepository
    {
        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SourceBook?> GetAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(book is not null &&
                            book.SourceId == sourceId &&
                            book.ExternalBookId == externalBookId
                ? book
                : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceBook>>(book is null ? [] : [book]);

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryArtifacts : IFetchArtifactRepository
    {
        public List<FetchArtifact> Store { get; } = [];

        public Task AddAsync(FetchArtifact artifact, CancellationToken cancellationToken = default)
        {
            Store.Add(artifact);
            return Task.CompletedTask;
        }

        public Task<FetchArtifact?> GetLatestAsync(
            string sourceId,
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<FetchArtifact?>(
                Store.Where(a => a.SourceId == sourceId && a.ExternalChapterId == externalChapterId)
                    .OrderByDescending(a => a.FetchedAt)
                    .FirstOrDefault());

        public Task<IReadOnlySet<string>> ListFetchedExternalChapterIdsAsync(
            string sourceId,
            IEnumerable<string> externalChapterIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(
                    Store.Where(a => a.SourceId == sourceId).Select(a => a.ExternalChapterId),
                    StringComparer.Ordinal));

        public Task<IReadOnlySet<string>> ListRecentlyFetchedExternalChapterIdsAsync(
            string sourceId,
            IEnumerable<string> externalChapterIds,
            DateTimeOffset since,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(
                    Store.Where(a => a.SourceId == sourceId && a.FetchedAt >= since)
                        .Select(a => a.ExternalChapterId),
                    StringComparer.Ordinal));
    }

    /// <summary>冲突判定与 EF 实现同语义:Pending/Leased/Running/DeadLettered 阻止,Completed 不阻止。</summary>
    private sealed class RecordingTaskRepository(HashSet<string> conflicts) : ICrawlerTaskRepository
    {
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

        public Task<CrawlerTask?> TryLeaseAsync(
            Guid taskId,
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;

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
            Task.FromResult(conflicts.Contains($"{capability}:{variableName}={variableValue}"));
    }
}
