using System.Text.Json;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Legado.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ContractTests;

/// <summary>
/// Legado 发布门禁：规则生成/JSON 结构以及 Search → BookInfo → TOC → Content 必须连续可用。
/// 夹具只模拟已落库的正典数据，不代表真实来源、真实客户端或人工验收证据。
/// </summary>
[TestClass]
public sealed class LegadoContractReleaseGateTests
{
    private static readonly DateTimeOffset FixtureTime =
        new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions WebJson =
        new(JsonSerializerDefaults.Web);

    [TestMethod]
    public async Task Release_Gate_Completes_The_Legado_V1_Flow()
    {
        var generator = new LegadoRuleGenerator();
        generator.Profile.Validate();

        using var manifest = JsonDocument.Parse(
            generator.Generate("https://inkflow.example.com"));
        AssertManifestShape(manifest.RootElement, generator.Profile);

        var fixture = await BuildFixtureAsync();
        var service = new LegadoContractService(fixture.Catalog);

        // Generate Rule -> JSON Validate -> Search.
        var search = await service.SearchAsync("契约测试");
        Assert.AreEqual(1, search.Count);
        Assert.AreEqual(fixture.BookId, search[0].BookId);
        Assert.AreEqual(
            $"/api/legado/v1/books/{fixture.BookId}",
            search[0].DetailUrl);
        AssertSearchPayload(search, fixture.BookId);

        // Search -> BookInfo.
        var book = await service.GetBookAsync(fixture.BookId);
        Assert.IsNotNull(book);
        Assert.AreEqual(fixture.BookId, book!.BookId);
        Assert.AreEqual(
            $"/api/legado/v1/books/{fixture.BookId}/chapters",
            book.TocUrl);
        AssertBookPayload(book);

        // BookInfo -> TOC.
        var toc = await service.GetTocAsync(fixture.BookId);
        Assert.IsNotNull(toc);
        var chapter = toc!.Single();
        Assert.AreEqual(fixture.ChapterId, chapter.ChapterId);
        Assert.AreEqual(
            $"/api/legado/v1/chapters/{fixture.ChapterId}",
            chapter.ChapterUrl);
        AssertTocPayload(toc, fixture.ChapterId);

        // TOC -> Content.
        var content = await service.GetChapterContentAsync(fixture.ChapterId);
        Assert.IsNotNull(content);
        Assert.AreEqual(fixture.ChapterId, content!.ChapterId);
        StringAssert.Contains(content.Content, "正文第一段");
        StringAssert.Contains(content.Content, "正文第二段");
        AssertContentPayload(content, fixture.ChapterId);
    }

    [TestMethod]
    public void Compatibility_Profile_Declares_The_Published_V1_Surface()
    {
        var profile = LegadoCompatibilityProfile.Current;

        profile.Validate();
        Assert.AreEqual("legado-book-source-v1", profile.SchemaVersion);
        Assert.AreEqual("3.0", profile.MinSupportedVersion);
        Assert.AreEqual("3.0", profile.TestedVersion);
        CollectionAssert.AreEquivalent(
            new[] { "search", "book-info", "toc", "content", "personal-token" },
            profile.Capabilities.ToArray());
        Assert.IsNull(profile.DeprecatedAt);
    }

    private static void AssertManifestShape(
        JsonElement root,
        LegadoCompatibilityProfile profile)
    {
        Assert.AreEqual("InkFlow / 墨流", root.GetProperty("bookSourceName").GetString());
        Assert.AreEqual(0, root.GetProperty("bookSourceType").GetInt32());
        Assert.AreEqual(
            "https://inkflow.example.com/api/legado/v1/search?q={{key}}",
            root.GetProperty("searchUrl").GetString());
        Assert.IsFalse(root.TryGetProperty("header", out _));

        var search = root.GetProperty("ruleSearch");
        Assert.AreEqual("$.data[*]", search.GetProperty("bookList").GetString());
        Assert.AreEqual("$.title", search.GetProperty("name").GetString());
        Assert.AreEqual("$.author", search.GetProperty("author").GetString());
        Assert.AreEqual("$.detailUrl", search.GetProperty("bookUrl").GetString());

        var bookInfo = root.GetProperty("ruleBookInfo");
        Assert.AreEqual("$.title", bookInfo.GetProperty("name").GetString());
        Assert.AreEqual("$.author", bookInfo.GetProperty("author").GetString());
        Assert.AreEqual("$.tocUrl", bookInfo.GetProperty("tocUrl").GetString());

        var toc = root.GetProperty("ruleToc");
        Assert.AreEqual("$.data[*]", toc.GetProperty("chapterList").GetString());
        Assert.AreEqual("$.title", toc.GetProperty("chapterName").GetString());
        Assert.AreEqual("$.chapterUrl", toc.GetProperty("chapterUrl").GetString());

        Assert.AreEqual("$.content", root.GetProperty("ruleContent").GetProperty("content").GetString());
        Assert.AreEqual("legado-book-source-v1", profile.SchemaVersion);
    }

    private static void AssertSearchPayload(
        IReadOnlyList<LegadoSearchItem> search,
        Guid bookId)
    {
        using var payload = JsonDocument.Parse(
            JsonSerializer.Serialize(new { data = search }, WebJson));
        var item = payload.RootElement.GetProperty("data").EnumerateArray().Single();

        Assert.AreEqual(bookId, item.GetProperty("bookId").GetGuid());
        Assert.IsFalse(string.IsNullOrWhiteSpace(item.GetProperty("title").GetString()));
        Assert.IsFalse(string.IsNullOrWhiteSpace(item.GetProperty("author").GetString()));
        Assert.IsTrue(item.GetProperty("detailUrl").GetString()!.StartsWith(
            "/api/legado/v1/books/", StringComparison.Ordinal));
    }

    private static void AssertBookPayload(LegadoBookInfo book)
    {
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(book, WebJson));
        var root = payload.RootElement;

        Assert.AreEqual(book.BookId, root.GetProperty("bookId").GetGuid());
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("author").GetString()));
        Assert.IsTrue(root.GetProperty("tocUrl").GetString()!.EndsWith(
            "/chapters", StringComparison.Ordinal));
    }

    private static void AssertTocPayload(
        IReadOnlyList<LegadoTocItem> toc,
        Guid chapterId)
    {
        using var payload = JsonDocument.Parse(
            JsonSerializer.Serialize(new { data = toc }, WebJson));
        var item = payload.RootElement.GetProperty("data").EnumerateArray().Single();

        Assert.AreEqual(chapterId, item.GetProperty("chapterId").GetGuid());
        Assert.AreEqual(0, item.GetProperty("index").GetInt32());
        Assert.IsFalse(string.IsNullOrWhiteSpace(item.GetProperty("title").GetString()));
        Assert.IsTrue(item.GetProperty("chapterUrl").GetString()!.StartsWith(
            "/api/legado/v1/chapters/", StringComparison.Ordinal));
    }

    private static void AssertContentPayload(LegadoContent content, Guid chapterId)
    {
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(content, WebJson));
        var root = payload.RootElement;

        Assert.AreEqual(chapterId, root.GetProperty("chapterId").GetGuid());
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        StringAssert.Contains(root.GetProperty("content").GetString()!, "正文第一段");
    }

    private static async Task<(CatalogQueryService Catalog, Guid BookId, Guid ChapterId)> BuildFixtureAsync()
    {
        var books = new InMemoryBooks();
        var versions = new InMemoryVersionRepository();
        var book = CanonicalBook.Create("契约测试书", "InkFlow", FixtureTime);
        book.AddChapter(0, "第一章 契约", FixtureTime);
        var chapter = book.Chapters.Single();

        await books.AddAsync(book);
        var publishing = await new ContentPublishingService(versions).PublishAsync(
            book.Id,
            chapter.Id,
            "contract-fixture-source",
            "<p>正文第一段。</p><p>正文第二段。</p>");
        Assert.IsTrue(publishing.IsSuccess);

        return (
            new CatalogQueryService(books, versions, new AllowAllContentPolicyReader()),
            book.Id,
            chapter.Id);
    }

    private sealed class AllowAllContentPolicyReader : IContentPolicyReader
    {
        public Task<bool> IsTakedownAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class InMemoryBooks : ICanonicalBookRepository
    {
        private readonly Dictionary<Guid, CanonicalBook> _store = [];

        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            _store[book.Id] = book;
            return Task.CompletedTask;
        }

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(id, out var book) ? book : null);

        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CanonicalBook>>(_store.Values.ToList());

        public Task<CanonicalBook?> FindByTitleAuthorAsync(
            string title,
            string author,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CanonicalBook?>(_store.Values.FirstOrDefault(book =>
                book.Title == title && book.Author == author));

        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            _store[book.Id] = book;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryVersionRepository : IContentVersionRepository
    {
        private readonly List<ContentVersion> _store = [];

        public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default)
        {
            _store.RemoveAll(existing =>
                existing.CanonicalChapterId == version.CanonicalChapterId && existing.IsCurrent);
            _store.Add(ContentVersion.Rehydrate(
                version.Id,
                version.CanonicalBookId,
                version.CanonicalChapterId,
                version.SourceId,
                version.CanonicalHash,
                version.CanonicalText,
                version.ParagraphCount,
                version.QualityScore,
                isCurrent: true,
                version.CreatedAt));
            return Task.CompletedTask;
        }

        public Task<ContentVersion?> FindByHashAsync(
            Guid canonicalChapterId,
            string canonicalHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentVersion?>(_store.FirstOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId &&
                version.CanonicalHash == canonicalHash));

        public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentVersion>>(_store
                .Where(version => version.CanonicalChapterId == canonicalChapterId)
                .ToList());

        public Task<ContentVersion?> GetCurrentForChapterAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentVersion?>(_store.FirstOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId && version.IsCurrent));

        public Task<Guid?> GetCurrentCanonicalBookIdAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(_store.FirstOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId && version.IsCurrent)?.CanonicalBookId);

        public Task SetCurrentAsync(
            Guid chapterId,
            Guid versionId,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _store.Count; i++)
            {
                var version = _store[i];
                if (version.CanonicalChapterId != chapterId)
                {
                    continue;
                }

                _store[i] = ContentVersion.Rehydrate(
                    version.Id,
                    version.CanonicalBookId,
                    version.CanonicalChapterId,
                    version.SourceId,
                    version.CanonicalHash,
                    version.CanonicalText,
                    version.ParagraphCount,
                    version.QualityScore,
                    version.Id == versionId,
                    version.CreatedAt);
            }

            return Task.CompletedTask;
        }
    }
}
