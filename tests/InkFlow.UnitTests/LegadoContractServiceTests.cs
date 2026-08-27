using System.Text.Json;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Legado.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class LegadoContractServiceTests
{
    private static (CatalogQueryService Catalog, InMemoryVersionRepository Versions) BuildCatalog(
        IContentPolicyReader? policyReader = null)
    {
        var books = new InMemoryBooks();
        var versions = new InMemoryVersionRepository();
        var chapterId = Guid.NewGuid();

        var book = CanonicalBook.Create("剑来", "烽火戏诸侯", T0);
        book.AddChapter(0, "第一章 惊蛰", T0);
        // 直接以已知章节 ID 重建,保证与发布内容对应。
        var rebuilt = CanonicalBook.Rehydrate(book.Id, "剑来", "烽火戏诸侯", T0, T0,
            [new CanonicalChapter(chapterId, book.Id, 0, "第一章 惊蛰", T0)]);
        books.Store[rebuilt.Id] = rebuilt;

        var published = new ContentPublishingService(versions).PublishAsync(
            rebuilt.Id, chapterId, "example-source",
            "<p>正文第一段。</p><p>正文第二段。</p>").Result;
        Assert.IsTrue(published.IsSuccess);

        return (
            new CatalogQueryService(
                books,
                versions,
                policyReader ?? new AllowAllContentPolicyReader()),
            versions);
    }

    private static readonly DateTimeOffset T0 = new(2026, 8, 27, 8, 30, 0, TimeSpan.Zero);

    private sealed class InMemoryVersionRepository : IContentVersionRepository
    {
        public List<ContentVersion> Store { get; } = [];

        public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default)
        {
            Store.Add(ContentVersion.Rehydrate(version.Id, version.CanonicalBookId,
                version.CanonicalChapterId, version.SourceId, version.CanonicalHash,
                version.CanonicalText, version.ParagraphCount, version.QualityScore,
                isCurrent: true, version.CreatedAt));
            return Task.CompletedTask;
        }

        public Task<ContentVersion?> FindByHashAsync(Guid canonicalChapterId, string canonicalHash, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentVersion?>(null);

        public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentVersion>>(Store.ToList());

        public Task<ContentVersion?> GetCurrentForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentVersion?>(
                Store.FirstOrDefault(v => v.CanonicalChapterId == canonicalChapterId && v.IsCurrent));

        public Task<Guid?> GetCurrentCanonicalBookIdAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(Store.FirstOrDefault(v =>
                v.CanonicalChapterId == canonicalChapterId && v.IsCurrent)?.CanonicalBookId);

        public Task SetCurrentAsync(Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class AllowAllContentPolicyReader : IContentPolicyReader
    {
        public Task<bool> IsTakedownAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class MutableContentPolicyReader : IContentPolicyReader
    {
        public HashSet<Guid> TakenDownBookIds { get; } = [];

        public Task<bool> IsTakedownAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TakenDownBookIds.Contains(canonicalBookId));
    }

    private sealed class InMemoryBooks : ICanonicalBookRepository
    {
        public Dictionary<Guid, CanonicalBook> Store { get; } = [];
        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }
        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue(id, out var b) ? b : null);
        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CanonicalBook>>(Store.Values.ToList());


        public Task<CanonicalBook?> FindByTitleAuthorAsync(string title, string author, CancellationToken cancellationToken = default)
            => Task.FromResult<CanonicalBook?>(null);        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task Search_Returns_Items_With_DetailUrls_And_Filters_By_Keyword()
    {
        var (catalog, _) = BuildCatalog();
        var service = new LegadoContractService(catalog);

        var all = await service.SearchAsync("");
        var hit = all.Single();

        Assert.AreEqual("剑来", hit.Title);
        StringAssert.Contains(hit.DetailUrl, "/api/legado/v1/books/");

        var filtered = await service.SearchAsync("不存在");
        Assert.AreEqual(0, filtered.Count);
    }

    [TestMethod]
    public async Task Book_Info_Contains_TocUrl()
    {
        var (catalog, _) = BuildCatalog();
        var service = new LegadoContractService(catalog);
        var all = await service.SearchAsync("");

        var info = await service.GetBookAsync(all[0].BookId);
        Assert.IsNotNull(info);
        StringAssert.Contains(info!.TocUrl, "/api/legado/v1/books/");
        StringAssert.Contains(info.TocUrl, "/chapters");
    }

    [TestMethod]
    public async Task Toc_Lists_Chapters_With_ChapterUrls()
    {
        var (catalog, _) = BuildCatalog();
        var service = new LegadoContractService(catalog);
        var all = await service.SearchAsync("");

        var toc = await service.GetTocAsync(all[0].BookId);
        Assert.IsNotNull(toc);
        Assert.AreEqual(1, toc!.Count);
        StringAssert.Contains(toc[0].ChapterUrl, "/api/legado/v1/chapters/");
        Assert.AreEqual("第一章 惊蛰", toc[0].Title);
    }

    [TestMethod]
    public async Task Chapter_Content_Is_Served_From_Current_Version()
    {
        var (catalog, _) = BuildCatalog();
        var service = new LegadoContractService(catalog);
        var all = await service.SearchAsync("");
        var toc = await service.GetTocAsync(all[0].BookId);

        var content = await service.GetChapterContentAsync(toc![0].ChapterId);
        Assert.IsNotNull(content);
        StringAssert.Contains(content!.Content, "正文第一段");
    }

    [TestMethod]
    public async Task Takedown_Hides_Book_From_Legado_Search_Info_And_Toc()
    {
        var policy = new MutableContentPolicyReader();
        var (catalog, _) = BuildCatalog(policy);
        var service = new LegadoContractService(catalog);
        var visible = (await service.SearchAsync("剑来")).Single();
        policy.TakenDownBookIds.Add(visible.BookId);

        Assert.AreEqual(0, (await service.SearchAsync("剑来")).Count);
        Assert.IsNull(await service.GetBookAsync(visible.BookId));
        Assert.IsNull(await service.GetTocAsync(visible.BookId));
    }

    [TestMethod]
    public void Manifest_Generates_Valid_Legado_Book_Source()
    {
        var json = LegadoBookSourceManifest.Generate("https://inkflow.example.com");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.AreEqual("InkFlow / 墨流", root.GetProperty("bookSourceName").GetString());
        Assert.AreEqual(0, root.GetProperty("bookSourceType").GetInt32());

        var searchUrl = root.GetProperty("searchUrl").GetString();
        Assert.IsTrue(searchUrl!.Contains("{{key}}"), $"searchUrl 应含 Legado 关键词占位符: {searchUrl}");
        Assert.IsTrue(searchUrl.StartsWith("https://inkflow.example.com/api/legado/v1/search"));
    }

    [TestMethod]
    public void Manifest_Rejects_Non_Http_Base_Url()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => LegadoBookSourceManifest.Generate("ftp://bad.example.com"));
    }
}
