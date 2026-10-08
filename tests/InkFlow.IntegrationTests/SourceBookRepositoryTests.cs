using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>来源书目仓储集成测试：真实 PostgreSQL 18 上验证迁移、唯一约束与章节幂等同步。</summary>
[TestClass]
public sealed class SourceBookRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static PostgreSqlContainer? _container;

    [ClassInitialize]
    public static async Task StartContainerAsync(TestContext _)
    {
        _container = new PostgreSqlBuilder(new DockerImage("postgres:18-alpine")).Build();
        await _container.StartAsync().ConfigureAwait(false);
    }

    [ClassCleanup]
    public static async Task StopContainerAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static EfSourceBookRepository CreateRepository()
    {
        var options = new DbContextOptionsBuilder<SourcesDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new SourcesDbContext(options);
        db.Database.Migrate();
        return new EfSourceBookRepository(db);
    }

    private static SourceBook NewBook(string externalId) =>
        SourceBook.Create("example-source", externalId, "剑来", "烽火戏诸侯", T0);

    [TestMethod]
    public async Task List_Page_Uses_Stable_Keyset_Cursor()
    {
        var repo = CreateRepository();
        var sourceId = $"page-source-{Guid.NewGuid():N}";
        var createdAt = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var books = Enumerable.Range(1, 3)
            .Select(index => SourceBook.Create(
                sourceId,
                $"book-{index}",
                $"书 {index}",
                "作者",
                createdAt))
            .ToArray();

        foreach (var book in books)
        {
            await repo.AddAsync(book).ConfigureAwait(false);
        }

        var first = await repo.ListPageAsync(null, 2).ConfigureAwait(false);
        var second = await repo.ListPageAsync(first.NextCursor, 2).ConfigureAwait(false);

        Assert.AreEqual(2, first.Books.Count);
        Assert.IsTrue(first.HasMore);
        Assert.IsNotNull(first.NextCursor);
        Assert.AreEqual(1, second.Books.Count);
        Assert.IsFalse(second.HasMore);
        Assert.IsNull(second.NextCursor);
        CollectionAssert.AreEquivalent(
            books.Select(book => book.ExternalBookId).ToArray(),
            first.Books.Concat(second.Books).Select(book => book.ExternalBookId).ToArray());
    }

    [TestMethod]
    public async Task First_Book_For_Source_Uses_Stable_Order_And_Filter()
    {
        var repo = CreateRepository();
        var sourceId = $"sample-source-{Guid.NewGuid():N}";
        var otherSource = $"other-source-{Guid.NewGuid():N}";
        var sourceBooks = new[]
        {
            SourceBook.Create(sourceId, "later", "later", "作者", T0.AddMinutes(1)),
            SourceBook.Create(sourceId, "earlier", "earlier", "作者", T0),
            SourceBook.Create(otherSource, "wrong-source", "wrong", "作者", T0.AddMinutes(-1)),
        };

        foreach (var book in sourceBooks)
        {
            await repo.AddAsync(book).ConfigureAwait(false);
        }

        var first = await repo.FindFirstForSourceAsync(sourceId).ConfigureAwait(false);

        Assert.IsNotNull(first);
        Assert.AreEqual("earlier", first.ExternalBookId);
        Assert.AreEqual(sourceId, first.SourceId);
        Assert.AreEqual(0, first.Chapters.Count);
    }

    [TestMethod]
    public async Task Book_With_Chapters_Roundtrips()
    {
        var repo = CreateRepository();
        var book = NewBook("10001");
        book.SyncChapters([("ch-001", "第一章"), ("ch-002", "第二章")], T0);
        await repo.AddAsync(book).ConfigureAwait(false);

        var loaded = await repo.GetAsync("example-source", "10001").ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("剑来", loaded.Title);
        Assert.AreEqual(2, loaded.Chapters.Count);
        Assert.AreEqual("ch-002", loaded.Chapters[1].ExternalChapterId);
    }

    [TestMethod]
    public async Task Chapter_Sync_Persists_Incrementally_And_Idempotently()
    {
        var repo = CreateRepository();
        var book = NewBook("20002");
        book.SyncChapters([("c1", "一")], T0);
        await repo.AddAsync(book).ConfigureAwait(false);

        // 第二批：新章节入库，旧章节保持原 ID。
        var loaded = (await repo.GetAsync("example-source", "20002").ConfigureAwait(false))!;
        var originalId = loaded.Chapters[0].Id;
        loaded.SyncChapters([("c1", "一"), ("c2", "二")], T0.AddMinutes(1));
        await repo.SaveAsync(loaded).ConfigureAwait(false);

        var reloaded = (await repo.GetAsync("example-source", "20002").ConfigureAwait(false))!;
        Assert.AreEqual(2, reloaded.Chapters.Count);
        Assert.AreEqual(originalId, reloaded.Chapters[0].Id);
        Assert.AreEqual("c2", reloaded.Chapters[1].ExternalChapterId);

        // 第三次同步（无新内容）不改变行数。
        reloaded.SyncChapters([("c1", "一"), ("c2", "二")], T0.AddMinutes(2));
        await repo.SaveAsync(reloaded).ConfigureAwait(false);
        var final = (await repo.GetAsync("example-source", "20002").ConfigureAwait(false))!;
        Assert.AreEqual(2, final.Chapters.Count);
    }
}
