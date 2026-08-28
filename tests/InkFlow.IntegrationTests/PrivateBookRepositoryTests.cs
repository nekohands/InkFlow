using DotNet.Testcontainers.Images;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>真实 PostgreSQL 上验证私有书目迁移、用户隔离和直接删除。</summary>
[TestClass]
public sealed class PrivateBookRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 20, 0, 0, TimeSpan.Zero);
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

    [TestMethod]
    public async Task Migration_Creates_Private_Book_Table()
    {
        await using var db = CreateDb();
        var tables = await db.Database.SqlQuery<string>(
                $"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'library'")
            .ToListAsync()
            .ConfigureAwait(false);

        CollectionAssert.Contains(tables.ToList(), "private_books");
        CollectionAssert.Contains(tables.ToList(), "private_chapters");
        Assert.IsFalse((await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).Any());
    }

    [TestMethod]
    public async Task Books_Roundtrip_And_Are_Isolated_By_User()
    {
        await using var db = CreateDb();
        var repository = new EfPrivateBookRepository(db);
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();
        var book = PrivateBook.Create(userA, "用户 A 的书", null, T0);

        await repository.AddAsync(book).ConfigureAwait(false);

        var loaded = await repository.GetAsync(userA, book.Id).ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("用户 A 的书", loaded!.Title);
        Assert.IsNull(await repository.GetAsync(userB, book.Id).ConfigureAwait(false));
        Assert.AreEqual(1, (await repository.ListAsync(userA, 100).ConfigureAwait(false)).Count);
        Assert.AreEqual(0, (await repository.ListAsync(userB, 100).ConfigureAwait(false)).Count);
    }

    [TestMethod]
    public async Task Update_And_Delete_Require_Owner()
    {
        await using var db = CreateDb();
        var repository = new EfPrivateBookRepository(db);
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();
        var book = PrivateBook.Create(userA, "旧名", null, T0);
        await repository.AddAsync(book).ConfigureAwait(false);

        book.UpdateMetadata("新名", "作者", T0.AddMinutes(1));
        Assert.IsFalse(await repository.SaveAsync(
            PrivateBook.Rehydrate(userB, book.Id, "越权", null, T0, T0.AddMinutes(1)))
            .ConfigureAwait(false));
        Assert.IsTrue(await repository.SaveAsync(book).ConfigureAwait(false));
        Assert.AreEqual("新名", (await repository.GetAsync(userA, book.Id).ConfigureAwait(false))!.Title);

        Assert.IsFalse(await repository.DeleteAsync(userB, book.Id).ConfigureAwait(false));
        Assert.IsTrue(await repository.DeleteAsync(userA, book.Id).ConfigureAwait(false));
        Assert.IsNull(await repository.GetAsync(userA, book.Id).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Imported_Chapters_Roundtrip_And_Require_Book_Owner()
    {
        await using var db = CreateDb();
        var repository = new EfPrivateBookRepository(db);
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();
        var book = PrivateBook.Create(userA, "导入书", "作者", T0);
        var chapter = PrivateChapter.Create(
            userA,
            book.Id,
            0,
            "第一章",
            PrivateContentDocument.FromParagraphs(["第一段", "第二段"]),
            T0);

        await repository.AddWithChaptersAsync(book, [chapter]).ConfigureAwait(false);

        var chapters = await repository
            .ListChaptersAsync(userA, book.Id)
            .ConfigureAwait(false);
        Assert.AreEqual(1, chapters.Count);
        Assert.AreEqual("第一段\n\n第二段", chapters[0].ContentText);
        Assert.IsNull(await repository
            .GetChapterAsync(userB, book.Id, chapter.Id)
            .ConfigureAwait(false));

        Assert.IsTrue(await repository.DeleteAsync(userA, book.Id).ConfigureAwait(false));
        Assert.AreEqual(0, (await repository
            .ListChaptersAsync(userA, book.Id)
            .ConfigureAwait(false)).Count);
    }

    private static LibraryDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new LibraryDbContext(options);
        db.Database.Migrate();
        return db;
    }
}
