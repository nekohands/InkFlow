using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>
/// Library 仓储集成测试：真实 PostgreSQL 18 上验证迁移、聚合持久化与章节增量写入。
/// 本机无 Docker 时无法运行，完整验证依赖远端 CI。
/// </summary>
[TestClass]
public sealed class CanonicalBookRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 15, 0, 0, TimeSpan.Zero);

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

    private static EfCanonicalBookRepository CreateRepository()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new LibraryDbContext(options);
        db.Database.Migrate();
        return new EfCanonicalBookRepository(db);
    }

    [TestMethod]
    public async Task Book_With_Chapters_Roundtrips()
    {
        var repo = CreateRepository();
        var book = CanonicalBook.Create("剑来", "烽火戏诸侯", T0);
        book.AddChapter(0, "第一章 惊蛰", T0);
        book.AddChapter(1, "第二章 开门", T0);

        await repo.AddAsync(book).ConfigureAwait(false);

        var loaded = await repo.GetAsync(book.Id).ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("剑来", loaded.Title);
        Assert.AreEqual(2, loaded.Chapters.Count);
        Assert.AreEqual("第一章 惊蛰", loaded.Chapters[0].Title);
    }

    [TestMethod]
    public async Task Save_Appends_New_Chapters_Without_Touching_Old_Ones()
    {
        var repo = CreateRepository();
        var book = CanonicalBook.Create("书", "作者", T0);
        book.AddChapter(0, "第一章", T0);
        await repo.AddAsync(book).ConfigureAwait(false);

        var loaded = (await repo.GetAsync(book.Id).ConfigureAwait(false))!;
        var originalChapterId = loaded.Chapters[0].Id;

        loaded.AddChapter(1, "第二章", T0.AddMinutes(5));
        loaded.UpdateMetadata("书（修订）", "作者", T0.AddMinutes(5));
        await repo.SaveAsync(loaded).ConfigureAwait(false);

        var reloaded = (await repo.GetAsync(book.Id).ConfigureAwait(false))!;
        Assert.AreEqual("书（修订）", reloaded.Title);
        Assert.AreEqual(2, reloaded.Chapters.Count);
        // 已有章节的稳定 ID 不变——阅读历史依赖它。
        Assert.AreEqual(originalChapterId, reloaded.Chapters[0].Id);
    }

    [TestMethod]
    public async Task Missing_Book_Returns_Null()
    {
        var repo = CreateRepository();
        var missing = await repo.GetAsync(Guid.NewGuid()).ConfigureAwait(false);
        Assert.IsNull(missing);
    }
}
