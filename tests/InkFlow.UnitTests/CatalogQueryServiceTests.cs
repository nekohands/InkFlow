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
        public int FullContentReads { get; private set; }

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
        {
            FullContentReads++;
            return Task.FromResult<ContentVersion?>(
                Store.FirstOrDefault(v => v.CanonicalChapterId == canonicalChapterId && v.IsCurrent));
        }

        public Task<Guid?> GetCurrentCanonicalBookIdAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(Store.FirstOrDefault(v =>
                v.CanonicalChapterId == canonicalChapterId && v.IsCurrent)?.CanonicalBookId);

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
    public async Task Search_Filters_By_Title_And_Author_Case_Insensitive()
    {
        var books = new InMemoryBookRepository();
        await books.AddAsync(CreateBook("剑来", "烽火戏诸侯", withChapters: false));
        await books.AddAsync(CreateBook("玉簟秋", "灵希", withChapters: false));

        var service = new CatalogQueryService(books, new InMemoryVersionRepository(), new AllowAllContentPolicyReader());

        // 书名包含匹配、作者包含匹配、大小写不敏感。
        Assert.AreEqual(1, (await service.SearchBooksAsync("剑来")).Count);
        Assert.AreEqual(1, (await service.SearchBooksAsync("灵希")).Count);

        var bookA = CanonicalBook.Create("UPPER Title", "lower author", T0);
        await books.AddAsync(bookA);
        Assert.AreEqual(1, (await service.SearchBooksAsync("upper")).Count,
            "书名匹配大小写不敏感");
        Assert.AreEqual(1, (await service.SearchBooksAsync("AUTHOR")).Count,
            "作者匹配大小写不敏感");

        // 空白关键词 = 浏览语义,返回全部。
        Assert.AreEqual(3, (await service.SearchBooksAsync("  ")).Count);
    }

    [TestMethod]
    public async Task Search_No_Match_Returns_Empty_List()
    {
        var books = new InMemoryBookRepository();
        await books.AddAsync(CreateBook("剑来", "烽火戏诸侯", withChapters: false));

        var service = new CatalogQueryService(books, new InMemoryVersionRepository(), new AllowAllContentPolicyReader());

        var hits = await service.SearchBooksAsync("不存在的书");

        Assert.AreEqual(0, hits.Count);
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

        var service = new CatalogQueryService(books, new InMemoryVersionRepository(), new AllowAllContentPolicyReader());
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

        var service = new CatalogQueryService(books, versions, new AllowAllContentPolicyReader());
        var content = await service.GetChapterContentAsync(chapterId);

        Assert.IsNotNull(content);
        Assert.AreEqual(3, content.Paragraphs.Count);
        Assert.AreEqual("第二段", content.Paragraphs[1]);
        Assert.AreEqual("example-source", content.SourceId);
    }

    [TestMethod]
    public async Task Takedown_Hides_Book_And_Blocks_Body_Load()
    {
        var books = new InMemoryBookRepository();
        var versions = new InMemoryVersionRepository();
        var book = CreateBook("受限书", "作者", withChapters: true);
        await books.AddAsync(book);
        var chapter = book.Chapters.Single();
        var published = await new ContentPublishingService(versions).PublishAsync(
            book.Id,
            chapter.Id,
            "example-source",
            "<p>不可公开正文</p>");
        Assert.IsTrue(published.IsSuccess);

        var policy = new SelectiveContentPolicyReader(book.Id);
        var service = new CatalogQueryService(books, versions, policy);

        Assert.AreEqual(0, (await service.ListBooksAsync()).Count);
        Assert.IsNull(await service.GetBookAsync(book.Id));
        Assert.IsNull(await service.GetChapterContentAsync(chapter.Id));
        Assert.AreEqual(0, versions.FullContentReads,
            "下架检查应在加载 CanonicalText 前完成");
    }

    [TestMethod]
    public async Task Chapter_Without_Published_Content_Returns_Null()
    {
        var service = new CatalogQueryService(
            new InMemoryBookRepository(),
            new InMemoryVersionRepository(),
            new AllowAllContentPolicyReader());
        Assert.IsNull(await service.GetChapterContentAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetBook_Missing_Returns_Null()
    {
        var service = new CatalogQueryService(
            new InMemoryBookRepository(),
            new InMemoryVersionRepository(),
            new AllowAllContentPolicyReader());
        Assert.IsNull(await service.GetBookAsync(Guid.NewGuid()));
    }

    /// <summary>构造带可选章节的书目聚合(复用既有测试约定)。</summary>
    private static CanonicalBook CreateBook(string title, string author, bool withChapters)
    {
        var book = CanonicalBook.Create(title, author, T0);
        if (withChapters)
        {
            book.AddChapter(0, $"{title}·第一章", T0);
        }

        return book;
    }

    private sealed class AllowAllContentPolicyReader : IContentPolicyReader
    {
        public Task<bool> IsTakedownAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class SelectiveContentPolicyReader(Guid takenDownBookId) : IContentPolicyReader
    {
        public Task<bool> IsTakedownAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canonicalBookId == takenDownBookId);
    }
}
