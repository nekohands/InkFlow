using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CatalogQueryServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 27, 8, 0, 0, TimeSpan.Zero);

    /// <summary>内存正典书仓储(含章节)。</summary>
    private sealed class InMemoryBookRepository : ICanonicalBookRepository
    {
        public Dictionary<Guid, CanonicalBook> Store { get; } = new();

        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue(id, out var book) ? book : null);

        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CanonicalBook>>(Store.Values.ToList());


        public Task<CanonicalBook?> FindByTitleAuthorAsync(string title, string author, CancellationToken cancellationToken = default)
            => Task.FromResult<CanonicalBook?>(null);
        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }
    }

    /// <summary>内存内容版本仓储,支持 IsCurrent 语义。</summary>
    private sealed class InMemoryVersionRepository : IContentVersionRepository
    {
        public List<ContentVersion> Store { get; } = [];

        public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default)
        {
            Store.Add(version);
            return Task.CompletedTask;
        }

        public Task<ContentVersion?> FindByHashAsync(Guid canonicalChapterId, string canonicalHash, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentVersion?>(
                Store.FirstOrDefault(v => v.CanonicalChapterId == canonicalChapterId && v.CanonicalHash == canonicalHash));

        public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentVersion>>(
                Store.Where(v => v.CanonicalChapterId == canonicalChapterId).ToList());

        public Task<ContentVersion?> GetCurrentForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentVersion?>(
                Store.FirstOrDefault(v => v.CanonicalChapterId == canonicalChapterId && v.IsCurrent));

        public Task SetCurrentAsync(Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
        {
            foreach (var v in Store.Where(v => v.CanonicalChapterId == chapterId).ToList())
            {
                var idx = Store.IndexOf(v);
                var isTarget = v.Id == versionId;
                Store[idx] = ContentVersion.Rehydrate(v.Id, v.CanonicalBookId,
                    v.CanonicalChapterId, v.SourceId, v.CanonicalHash,
                    v.CanonicalText, v.ParagraphCount, v.QualityScore,
                    isCurrent: isTarget, v.CreatedAt);
            }

            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task ListBooks_Returns_All_Books_With_Chapter_Counts()
    {
        var books = new InMemoryBookRepository();
        var bookA = CanonicalBook.Create("书A", "作者A", T0);
        bookA.AddChapter(0, "A-第一章", T0);
        var bookB = CanonicalBook.Create("书B", "作者B", T0);
        await books.AddAsync(bookA);
        await books.AddAsync(bookB);

        var service = new CatalogQueryService(books, new InMemoryVersionRepository());
        var list = await service.ListBooksAsync();

        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(1, list.Single(b => b.Title == "书A").ChapterCount);
        Assert.AreEqual(0, list.Single(b => b.Title == "书B").ChapterCount);
    }

    [TestMethod]
    public async Task GetChapterContent_Returns_Current_Version_Paragraphs()
    {
        var books = new InMemoryBookRepository();
        var versions = new InMemoryVersionRepository();
        var chapterId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var published = await new ContentPublishingService(versions).PublishAsync(
            bookId, chapterId, "example-source",
            "<p>第一段</p><p>第二段</p><p>第三段</p>");
        Assert.IsTrue(published.IsSuccess);

        // 正典书需要包含该章节以便返回标题/序号。
        var book = CanonicalBook.Create("书", "作者", T0);
        await books.AddAsync(book);

        var service = new CatalogQueryService(books, versions);
        var content = await service.GetChapterContentAsync(chapterId);

        Assert.IsNotNull(content);
        Assert.AreEqual(3, content.Paragraphs.Count);
        Assert.AreEqual("第二段", content.Paragraphs[1]);
        Assert.AreEqual("example-source", content.SourceId);
    }

    [TestMethod]
    public async Task Chapter_Without_Published_Content_Returns_Null()
    {
        var service = new CatalogQueryService(new InMemoryBookRepository(), new InMemoryVersionRepository());
        Assert.IsNull(await service.GetChapterContentAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetBook_Missing_Returns_Null()
    {
        var service = new CatalogQueryService(new InMemoryBookRepository(), new InMemoryVersionRepository());
        Assert.IsNull(await service.GetBookAsync(Guid.NewGuid()));
    }
}
