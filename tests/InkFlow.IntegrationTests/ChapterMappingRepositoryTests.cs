using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>章节映射仓储集成测试:真实 PostgreSQL 18 上验证迁移与唯一约束。</summary>
[TestClass]
public sealed class ChapterMappingRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 21, 0, 0, TimeSpan.Zero);

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

    private static (EfChapterMappingRepository Repo, LibraryDbContext Db) CreateRepository()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new LibraryDbContext(options);
        db.Database.Migrate();
        return (new EfChapterMappingRepository(db), db);
    }

    [TestMethod]
    public async Task Mapping_Roundtrips_And_Is_Per_Source_Isolated()
    {
        var (repo, _) = CreateRepository();
        var mapping = new ChapterMapping(
            Guid.NewGuid(), "example-source", "ch-001",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), T0);
        await repo.AddAsync(mapping).ConfigureAwait(false);

        var loaded = await repo.FindAsync("example-source", "ch-001").ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(mapping.CanonicalChapterId, loaded.CanonicalChapterId);
        Assert.IsNull(await repo.FindAsync("other-source", "ch-001").ConfigureAwait(false));
    }
}
