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
        ISourceHealthRecorder? healthRecorder = null)
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
            TimeProvider.System,
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
    public async Task Same_Content_Second_Fetch_Is_Unchanged_And_Skips_Storage()
    {
        var (service, artifacts, _) = CreateService("<p>第一章正文</p>");
        await service.FetchChapterContentAsync("example-source", "10001", "ch-001");

        var second = await service.FetchChapterContentAsync("example-source", "10001", "ch-001");

        Assert.IsTrue(second.IsSuccess);
        Assert.IsTrue(second.Unchanged, "内容未变应返回 Unchanged");
        Assert.AreEqual(1, artifacts.Store.Count, "未变的内容不应产生新的存储行");
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
