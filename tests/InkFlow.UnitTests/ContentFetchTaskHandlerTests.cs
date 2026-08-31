using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

/// <summary>
/// Content 任务处理器与发布桥的编排验证:
/// 成功抓取把原文交给发布桥;不可发布(未映射)静默完成;
/// 发布异常转任务失败走重试;抓取失败短路不触达发布桥。全部内存执行。
/// </summary>
[TestClass]
public sealed class ContentFetchTaskHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private const string ChapterBody = "<p>修订后的第一章正文</p>";

    [TestMethod]
    public async Task Successful_Fetch_Forwards_Raw_Content_To_Publisher()
    {
        var harness = CreateHandler(new RecordingPublisher());

        var outcome = await harness.Handler.ExecuteAsync(CreateTask());

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        var call = harness.Publisher!.Calls.Single();
        Assert.AreEqual("example-source", call.SourceId);
        Assert.AreEqual("10001", call.BookId);
        Assert.AreEqual("ch-001", call.ChapterId);
        Assert.AreEqual(ChapterBody, call.RawContent);
        Assert.AreEqual(1, harness.Artifacts.Store.Count, "抓取产物应幂等落库");
    }

    [TestMethod]
    public async Task Unpublishable_Chapter_Completes_Without_Failure()
    {
        // 章节尚未映射到正典身份:桥返回 false,任务不算失败(避免无意义重试到死信)。
        var harness = CreateHandler(new RecordingPublisher { Result = false });

        var outcome = await harness.Handler.ExecuteAsync(CreateTask());

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.AreEqual(1, harness.Artifacts.Store.Count);
    }

    [TestMethod]
    public async Task Publisher_Exception_Fails_Task_For_Retry()
    {
        // 基础设施/发布故障必须让任务失败,由既有重试退避链处理。
        var harness = CreateHandler(new RecordingPublisher
        {
            Throw = new InvalidOperationException("boom"),
        });

        var outcome = await harness.Handler.ExecuteAsync(CreateTask());

        Assert.IsFalse(outcome.Succeeded);
        Assert.AreEqual("content publish failed.", outcome.FailureReason);
        Assert.IsFalse(outcome.FailureReason!.Contains("boom", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Fetch_Errors_Short_Circuit_Before_Publishing()
    {
        var harness = CreateHandler(new RecordingPublisher(), chapterBody: null);

        var outcome = await harness.Handler.ExecuteAsync(CreateTask());

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "no content");
        Assert.AreEqual(0, harness.Publisher!.Calls.Count, "抓取失败不得触达发布桥");
        Assert.AreEqual(0, harness.Artifacts.Store.Count);
    }

    [TestMethod]
    public async Task Handler_Without_Publisher_Only_Persists_Artifact()
    {
        // 未装配发布桥时保持旧行为:只落产物元数据(向后兼容)。
        var harness = CreateHandler(publisher: null);

        var outcome = await harness.Handler.ExecuteAsync(CreateTask());

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.AreEqual(1, harness.Artifacts.Store.Count);
    }

    [TestMethod]
    public async Task Credential_Reference_Flows_Through_The_Active_Content_Handler()
    {
        var harness = CreateHandler(publisher: null);

        var outcome = await harness.Handler.ExecuteAsync(CreateTask("source-credential"));

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.AreEqual("example-source", harness.Adapter.LastExecutionContext!.SourceId);
        Assert.AreEqual("source-credential", harness.Adapter.LastExecutionContext.CredentialReferenceId);
    }

    private static CrawlerTask CreateTask(string? credentialReferenceId = null) =>
        CrawlerTask.Create(
            new CrawlPayload(
                "example-source",
                SourceCapability.Content,
                new Dictionary<string, string>
                {
                    ["bookId"] = "10001",
                    ["chapterId"] = "ch-001",
                    ["reason"] = "refetch",
                },
                credentialReferenceId),
            createdAt: T0);

    private static Harness CreateHandler(IChainedContentPublisher? publisher, string? chapterBody = ChapterBody)
    {
        var books = new InMemoryBookRepository();
        books.Book!.SyncChapters([("ch-001", "第一章")], T0);

        var artifacts = new InMemoryArtifactRepository();
        var adapter = new FixedAdapter(chapterBody);
        var service = new SourceContentService(
            new FixedAdapterFactory(adapter),
            books,
            artifacts,
            TimeProvider.System);

        var recordingPublisher = publisher as RecordingPublisher;
        return new Harness(
            new ContentFetchTaskHandler(service, publisher),
            artifacts,
            recordingPublisher,
            adapter);
    }

    private sealed record Harness(
        ContentFetchTaskHandler Handler,
        InMemoryArtifactRepository Artifacts,
        RecordingPublisher? Publisher,
        FixedAdapter Adapter);

    /// <summary>记录调用并按预设返回/抛出的发布桥替身。</summary>
    private sealed class RecordingPublisher : IChainedContentPublisher
    {
        public List<(string SourceId, string BookId, string ChapterId, string RawContent)> Calls { get; } = [];

        public bool Result { get; set; } = true;

        public Exception? Throw { get; set; }

        public Task<bool> TryPublishAsync(
            string sourceId,
            string externalBookId,
            string externalChapterId,
            string rawContent,
            CancellationToken cancellationToken = default)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

            Calls.Add((sourceId, externalBookId, externalChapterId, rawContent));
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedAdapterFactory(ISourceAdapter? adapter) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(string sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(adapter is not null && adapter.SourceId == sourceId ? adapter : null);
    }

    private sealed class FixedAdapter(string? content) : ISourceAdapter
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
            Task.FromResult<SourceBookInfo?>(null);

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceTocEntry>>([]);

        public Task<string?> GetChapterContentAsync(
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(content);

        public Task<string?> GetChapterContentAsync(
            string externalChapterId,
            CancellationToken cancellationToken,
            SourceExecutionContext? executionContext)
        {
            LastExecutionContext = executionContext;
            return Task.FromResult(content);
        }
    }

    private sealed class InMemoryBookRepository : ISourceBookRepository
    {
        public SourceBook Book { get; } =
            SourceBook.Create("example-source", "10001", "剑来", "烽火戏诸侯", T0);

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SourceBook?> GetAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceBook?>(
                sourceId == Book.SourceId && externalBookId == Book.ExternalBookId ? Book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceBook>>([Book]);

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryArtifactRepository : IFetchArtifactRepository
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
}
