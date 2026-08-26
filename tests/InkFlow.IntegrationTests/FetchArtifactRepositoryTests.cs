using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>FetchArtifact 仓储集成测试：真实 PostgreSQL 18 上验证迁移与最新产物查询。</summary>
[TestClass]
public sealed class FetchArtifactRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 16, 0, 0, TimeSpan.Zero);

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

    private static EfFetchArtifactRepository CreateRepository()
    {
        var options = new DbContextOptionsBuilder<SourcesDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new SourcesDbContext(options);
        db.Database.Migrate();
        return new EfFetchArtifactRepository(db);
    }

    [TestMethod]
    public async Task GetLatest_Returns_Most_Recent_Artifact()
    {
        var repo = CreateRepository();

        await repo.AddAsync(FetchArtifact.Capture("src", "b1", "c1", "版本一", T0)).ConfigureAwait(false);
        await repo.AddAsync(FetchArtifact.Capture("src", "b1", "c1", "版本二", T0.AddMinutes(5))).ConfigureAwait(false);

        var latest = await repo.GetLatestAsync("src", "c1").ConfigureAwait(false);
        Assert.IsNotNull(latest);

        var expected = FetchArtifact.Capture("src", "b1", "c1", "版本二", T0).RawHash;
        Assert.AreEqual(expected, latest.RawHash);
    }

    [TestMethod]
    public async Task Latest_Is_Per_Chapter_Isolated()
    {
        var repo = CreateRepository();
        await repo.AddAsync(FetchArtifact.Capture("src", "b1", "c1", "第一章内容", T0)).ConfigureAwait(false);
        await repo.AddAsync(FetchArtifact.Capture("src", "b1", "c2", "第二章内容", T0)).ConfigureAwait(false);

        var latestC1 = await repo.GetLatestAsync("src", "c1").ConfigureAwait(false);
        Assert.IsNotNull(latestC1);

        // 用确定性哈希验证隔离：c1 的最新产物哈希应等于 c1 内容的哈希。
        var expected = FetchArtifact.Capture("src", "b1", "c1", "第一章内容", T0).RawHash;
        Assert.AreEqual(expected, latestC1.RawHash);
    }

    [TestMethod]
    public async Task Unknown_Chapter_Returns_Null()
    {
        var repo = CreateRepository();
        Assert.IsNull(await repo.GetLatestAsync("src", "never-fetched").ConfigureAwait(false));
    }
}
