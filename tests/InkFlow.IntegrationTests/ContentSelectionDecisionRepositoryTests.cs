using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>Content 选择审计记录集成测试：验证迁移、复合查询索引所依赖的表结构和往返。</summary>
[TestClass]
public sealed class ContentSelectionDecisionRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 27, 20, 0, 0, TimeSpan.Zero);

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

    private static EfContentSelectionDecisionRepository CreateRepository()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new ContentDbContext(options);
        db.Database.Migrate();
        return new EfContentSelectionDecisionRepository(db);
    }

    [TestMethod]
    public async Task Latest_Selection_Decision_Roundtrips()
    {
        var repo = CreateRepository();
        var chapterId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var decision = ContentSelectionDecision.Create(
            chapterId,
            versionId,
            "algorithm=content-selection-v1;fallback=false",
            T0);

        await repo.AddAsync(decision).ConfigureAwait(false);

        var loaded = await repo.GetLatestAsync(chapterId).ConfigureAwait(false);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(versionId, loaded.SelectedVersionId);
        Assert.AreEqual(ContentSelectionAlgorithm.Version, loaded.AlgorithmVersion);
        StringAssert.Contains(loaded.Evidence, "fallback=false");
    }
}
