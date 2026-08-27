using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceContentServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 15, 0, 0, TimeSpan.Zero);

    private sealed class InMemoryBookRepository : ISourceBookRepository
    {
        public SourceBook? Book { get; set; } =
            SourceBook.Create("example-source", "10001", "剑来", "烽火戏诸侯", T0);

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Book = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult(Book is not null && Book.SourceId == sourceId && Book.ExternalBookId == externalBookId ? Book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SourceBook>>(Book is null ? [] : [Book]);

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

        public Task<FetchArtifact?> GetLatestAsync(string sourceId, string externalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<FetchArtifact?>(
                Store.Where(a => a.SourceId == sourceId && a.ExternalChapterId == externalChapterId)
                    .OrderByDescending(a => a.FetchedAt)
                    .FirstOrDefault());

        public Task<IReadOnlySet<string>> ListFetchedExternalChapterIdsAsync(
            string sourceId,
            IEnumerable<string> externalChapterIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(
                    Store.Where(a => a.SourceId == sourceId).Select(a => a.ExternalChapterId),
                    StringComparer.Ordinal));

        public Task<IReadOnlySet<string>> ListRecentlyFetchedExternalChapterIdsAsync(
            string sourceId,
            IEnumerable<string> externalChapterIds,
            DateTimeOffset since,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(
                    Store.Where(a => a.SourceId == sourceId && a.FetchedAt >= since)
                        .Select(a => a.ExternalChapterId),
                    StringComparer.Ordinal));
    }

    /// <summary>固定正文返回的适配器。</summary>
    private sealed class FixedAdapter(string? content) : ISourceAdapter
    {
        public string SourceId => "example-source";
        public string? Content { get; set; } = content;
        public int ContentCallCount { get; private set; }

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SourceSearchResult>>([]);

        public Task<SourceBookInfo?> GetBookInfoAsync(string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult<SourceBookInfo?>(null);

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SourceTocEntry>>([]);

        public Task<string?> GetChapterContentAsync(string externalChapterId, CancellationToken cancellationToken = default)
        {
            ContentCallCount++;
            return Task.FromResult<string?>(Content);
        }
    }

    private static (SourceContentService Service, InMemoryArtifactRepository Artifacts, FixedAdapter Adapter) CreateService(
        string chapterBody,
        ISourceHealthReader? healthReader = null,
        ISourceHealthRecorder? healthRecorder = null,
        TimeProvider? clock = null)
    {
        var books = new InMemoryBookRepository();
        books.Book!.SyncChapters([("ch-001", "第一章")], T0);

        var artifacts = new InMemoryArtifactRepository();
        var adapter = new FixedAdapter(chapterBody);
        var factory = new FixedAdapterFactory(adapter);
        var service = new SourceContentService(
            factory,
            books,
            artifacts,
            clock ?? TimeProvider.System,
            healthReader,
            healthRecorder);
        return (service, artifacts, adapter);
    }

    private sealed class FixedAdapterFactory(ISourceAdapter? adapter) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<ISourceAdapter?>(adapter is not null && adapter.SourceId == sourceId ? adapter : null);
    }

    [TestMethod]
    public async Task First_Fetch_Persists_Artifact()
    {
        var (service, artifacts, _) = CreateService("<p>第一章正文</p>");

        var outcome = await service.FetchChapterContentAsync("example-source", "10001", "ch-001");

        Assert.IsTrue(outcome.IsSuccess, string.Join("; ", outcome.Errors));
        Assert.IsFalse(outcome.Unchanged);
        Assert.AreEqual(1, artifacts.Store.Count);
    }

    [TestMethod]
    public async Task Same_Content_Recheck_Is_Unchanged_And_Renews_Freshness_Anchor()
    {
        // 每次调用时钟前进 10 分钟,模拟两次相隔一段时间的抓取。
        var (service, artifacts, _) = CreateService(
            "<p>第一章正文</p>",
            clock: new SteppedClock(T0, TimeSpan.FromMinutes(10)));

        var first = await service.FetchChapterContentAsync("example-source", "10001", "ch-001");
        Assert.IsTrue(first.IsSuccess);
        Assert.IsFalse(first.Unchanged);
        Assert.AreEqual(1, artifacts.Store.Count);

        var second = await service.FetchChapterContentAsync("example-source", "10001", "ch-001");

        // 复检(哈希一致)返回 Unchanged,但同样落一条相同哈希的真实抓取记录:
        // 最新产物时间表示最近一次核查而非首次发现,作为修订重扫的保鲜锚点。
        Assert.IsTrue(second.IsSuccess);
        Assert.IsTrue(second.Unchanged, "内容未变应返回 Unchanged");
        Assert.AreEqual(2, artifacts.Store.Count, "复检应落一条相同哈希的产物行以续期保鲜锚点");
        Assert.AreEqual(artifacts.Store[0].RawHash, artifacts.Store[1].RawHash);
        Assert.IsTrue(
            artifacts.Store[1].FetchedAt > artifacts.Store[0].FetchedAt,
            "复检行的 FetchedAt 必须晚于首抓,否则保鲜判定无法续期");
        Assert.AreEqual(artifacts.Store[1].FetchedAt, second.Artifact!.FetchedAt);
        Assert.AreEqual("<p>第一章正文</p>", second.RawContent, "原文必须回传供发布桥使用");
    }

    [TestMethod]
    public async Task Changed_Content_Creates_New_Artifact()
    {
        var (service, artifacts, adapter) = CreateService("<p>旧正文</p>");
        await service.FetchChapterContentAsync("example-source", "10001", "ch-001");

        adapter.Content = "<p>修订后的正文</p>";
        var second = await service.FetchChapterContentAsync("example-source", "10001", "ch-001");

        Assert.IsTrue(second.IsSuccess);
        Assert.IsFalse(second.Unchanged);
        Assert.AreNotEqual(artifacts.Store[0].RawHash, second.Artifact!.RawHash);
    }

    [TestMethod]
    public async Task Unknown_Chapter_Fails_Before_Any_Network_Call()
    {
        var (service, _, artifacts) = CreateService("<p>正文</p>");

        var outcome = await service.FetchChapterContentAsync("example-source", "10001", "ghost-chapter");

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "not part of book");
    }

    [TestMethod]
    public async Task Empty_Content_Is_Reported_As_Error()
    {
        var (service, _, artifacts) = CreateService("");

        var outcome = await service.FetchChapterContentAsync("example-source", "10001", "ch-001");

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "no content");
    }

    [TestMethod]
    public async Task Content_Health_Is_Recorded_And_Unavailable_Source_Is_Not_Contacted()
    {
        var health = new RecordingHealth();
        var (service, _, _) = CreateService("<p>正文</p>", healthRecorder: health);

        var success = await service.FetchChapterContentAsync("example-source", "10001", "ch-001");

        Assert.IsTrue(success.IsSuccess);
        Assert.IsTrue(health.Calls.Any(call =>
            call.Capability == SourceCapability.Content && call.Succeeded));

        var blocked = new AlwaysUnavailableHealth();
        var blockedParts = CreateService("<p>不会请求</p>", blocked);
        var blockedService = blockedParts.Service;
        var blockedOutcome = await blockedService
            .FetchChapterContentAsync("example-source", "10001", "ch-001");

        Assert.IsFalse(blockedOutcome.IsSuccess);
        StringAssert.Contains(blockedOutcome.Errors[0], "unavailable");
        Assert.AreEqual(0, blockedParts.Adapter.ContentCallCount, "被阻断的来源不应触发上游正文请求");
    }

    /// <summary>每次调用前进固定步长的时钟,用于复检时间推进的可控断言。</summary>
    private sealed class SteppedClock(DateTimeOffset start, TimeSpan step) : TimeProvider
    {
        private DateTimeOffset _current = start;

        public override DateTimeOffset GetUtcNow()
        {
            var now = _current;
            _current += step;
            return now;
        }
    }

    private sealed class RecordingHealth : ISourceHealthRecorder
    {
        public List<(SourceCapability Capability, bool Succeeded, string? Reason)> Calls { get; } = [];

        public Task<SourceCapabilityHealth> RecordSuccessAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((capability, true, null));
            return Task.FromResult(SourceCapabilityHealth.Create(sourceId, capability, T0));
        }

        public Task<SourceCapabilityHealth> RecordFailureAsync(
            string sourceId,
            SourceCapability capability,
            string reason,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((capability, false, reason));
            return Task.FromResult(SourceCapabilityHealth.Create(sourceId, capability, T0));
        }
    }

    private sealed class AlwaysUnavailableHealth : ISourceHealthReader
    {
        public Task<bool> IsAvailableAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
