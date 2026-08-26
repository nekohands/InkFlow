using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceCatalogServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 11, 0, 0, TimeSpan.Zero);

    /// <summary>固定返回同一适配器的工厂。</summary>
    private sealed class FixedAdapterFactory(ISourceAdapter? adapter) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(adapter is not null && adapter.SourceId == sourceId ? adapter : null);
    }

    /// <summary>内存适配器:按预设目录/元数据响应。</summary>
    private sealed class FakeAdapter : ISourceAdapter
    {
        public string SourceId { get; init; } = "example-source";
        public string InfoTitle { get; set; } = "剑来";
        public string InfoAuthor { get; set; } = "烽火戏诸侯";
        public List<SourceTocEntry> Toc { get; set; } =
        [
            new("c1", 0, "第一章"),
            new("c2", 1, "第二章"),
        ];

        public int TocCallCount { get; private set; }

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(string keyword, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SourceSearchResult>>([]);

        public Task<SourceBookInfo?> GetBookInfoAsync(string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult<SourceBookInfo?>(new SourceBookInfo(InfoTitle, InfoAuthor));

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(string externalBookId, CancellationToken cancellationToken = default)
        {
            TocCallCount++;
            return Task.FromResult<IReadOnlyList<SourceTocEntry>>(Toc);
        }

        public Task<string?> GetChapterContentAsync(string externalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    /// <summary>内存书目仓储。</summary>
    private sealed class InMemoryBookRepository : ISourceBookRepository
    {
        public Dictionary<(string SourceId, string ExternalId), SourceBook> Store { get; } = [];

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue((sourceId, externalBookId), out var book) ? book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SourceBook>>(Store.Values.ToList());

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task Import_Creates_Then_Updates_The_Same_Book()
    {
        var repo = new InMemoryBookRepository();
        var adapter = new FakeAdapter();
        var service = new SourceCatalogService(new FixedAdapterFactory(adapter), repo, TimeProvider.System);

        var first = await service.ImportBookInfoAsync("example-source", "10001");
        Assert.IsTrue(first.IsSuccess);
        Assert.AreEqual(1, repo.Store.Count);

        adapter.InfoTitle = "剑来(修订)";
        var second = await service.ImportBookInfoAsync("example-source", "10001");
        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual("剑来(修订)", second.Book!.Title);
        Assert.AreEqual(first.Book!.Id, second.Book!.Id);
        Assert.AreEqual(1, repo.Store.Count);
    }

    [TestMethod]
    public async Task SyncChapters_Persists_Toc_And_Is_Idempotent()
    {
        var repo = new InMemoryBookRepository();
        var adapter = new FakeAdapter();
        var service = new SourceCatalogService(new FixedAdapterFactory(adapter), repo, TimeProvider.System);

        await service.ImportBookInfoAsync("example-source", "10001");
        var sync1 = await service.SyncChaptersAsync("example-source", "10001");
        Assert.IsTrue(sync1.IsSuccess, string.Join("; ", sync1.Errors));
        Assert.AreEqual(2, sync1.Book!.Chapters.Count);

        await service.SyncChaptersAsync("example-source", "10001");
        var reloaded = await repo.GetAsync("example-source", "10001");
        Assert.AreEqual(2, reloaded!.Chapters.Count, "重复同步不得产生重复章节");
    }

    [TestMethod]
    public async Task Unknown_Source_Fails_Clearly()
    {
        var service = new SourceCatalogService(
            new FixedAdapterFactory(null), new InMemoryBookRepository(), TimeProvider.System);

        var outcome = await service.ImportBookInfoAsync("ghost-source", "1");

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "does not exist or has no adapter");
    }
}
