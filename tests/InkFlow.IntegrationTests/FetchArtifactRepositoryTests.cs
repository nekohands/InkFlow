using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>FetchArtifact 仓储集成测试：真实 PostgreSQL 18 上验证迁移、最新产物查询，
/// 以及追更联动使用的批量存在性查询（只有"该来源已有产物"的章节会被排除）。</summary>
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
        // 使用本测试专属的来源/章节标识,避免与其他测试的数据相互干扰。
        await repo.AddAsync(FetchArtifact.Capture("iso-src", "b1", "iso-c1", "第一章内容", T0)).ConfigureAwait(false);
        await repo.AddAsync(FetchArtifact.Capture("iso-src", "b1", "iso-c2", "第二章内容", T0)).ConfigureAwait(false);

        var latestC1 = await repo.GetLatestAsync("iso-src", "iso-c1").ConfigureAwait(false);
        Assert.IsNotNull(latestC1);

        // 用确定性哈希验证隔离:c1 的最新产物哈希应等于 c1 内容的哈希。
        var expected = FetchArtifact.Capture("iso-src", "b1", "iso-c1", "第一章内容", T0).RawHash;
        Assert.AreEqual(expected, latestC1.RawHash);
        Assert.IsNull(await repo.GetLatestAsync("iso-src", "never").ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Unknown_Chapter_Returns_Null()
    {
        var repo = CreateRepository();
        Assert.IsNull(await repo.GetLatestAsync("src", "never-fetched").ConfigureAwait(false));
    }

    [TestMethod]
    public async Task ListFetchedExternalChapterIds_Returns_Only_Fetched_Chapters_Of_The_Source()
    {
        var repo = CreateRepository();

        await repo.AddAsync(FetchArtifact.Capture("chain-src", "book-1", "ch-1", "正文一", T0)).ConfigureAwait(false);
        // 同章节第二次抓取(内容修订)→ 新版本行,不改变存在性结论。
        await repo.AddAsync(FetchArtifact.Capture("chain-src", "book-1", "ch-2", "正文二", T0.AddMinutes(1))).ConfigureAwait(false);
        await repo.AddAsync(FetchArtifact.Capture("chain-src", "book-1", "ch-2", "正文二修订", T0.AddMinutes(2))).ConfigureAwait(false);
        // 另一来源的同 ID 章节,不得影响 chain-src 的判定。
        await repo.AddAsync(FetchArtifact.Capture("other-chain-src", "book-1", "ch-3", "别家正文", T0)).ConfigureAwait(false);

        var fetched = await repo
            .ListFetchedExternalChapterIdsAsync(
                "chain-src", ["ch-1", "ch-2", "ch-3", "ch-new"])
            .ConfigureAwait(false);

        CollectionAssert.AreEquivalent(new[] { "ch-1", "ch-2" }, fetched.ToList());
    }

    [TestMethod]
    public async Task ListFetchedExternalChapterIds_Empty_Input_Returns_Empty()
    {
        var repo = CreateRepository();

        var fetched = await repo
            .ListFetchedExternalChapterIdsAsync("any-src", Array.Empty<string>())
            .ConfigureAwait(false);

        Assert.AreEqual(0, fetched.Count);
    }

    [TestMethod]
    public async Task ListRecentlyFetchedExternalChapterIds_Filters_By_Time_And_Source()
    {
        var repo = CreateRepository();
        var now = T0.AddDays(30);

        // 近期产物:7 天内复检/首抓。
        await repo.AddAsync(FetchArtifact.Capture("recent-src", "b1", "ch-fresh", "新鲜正文", now.AddDays(-1))).ConfigureAwait(false);
        await repo.AddAsync(FetchArtifact.Capture("recent-src", "b1", "ch-refreshed", "早期版本", T0)).ConfigureAwait(false);
        await repo.AddAsync(FetchArtifact.Capture("recent-src", "b1", "ch-refreshed", "复检同哈希新行", now.AddDays(-2))).ConfigureAwait(false);
        // 过期产物:最后一次抓取早于 since。
        await repo.AddAsync(FetchArtifact.Capture("recent-src", "b1", "ch-stale", "过期正文", T0)).ConfigureAwait(false);
        // 另一来源的同 ID 章节不得影响 recent-src 的判定。
        await repo.AddAsync(FetchArtifact.Capture("other-recent-src", "b1", "ch-fresh", "别家近期", now.AddDays(-1))).ConfigureAwait(false);

        var recent = await repo
            .ListRecentlyFetchedExternalChapterIdsAsync(
                "recent-src", ["ch-fresh", "ch-refreshed", "ch-stale"], since: now.AddDays(-7))
            .ConfigureAwait(false);

        CollectionAssert.AreEquivalent(new[] { "ch-fresh", "ch-refreshed" }, recent.ToList());
        Assert.IsFalse(recent.Contains("ch-stale"), "最后一次抓取早于保鲜期的章节应判定为 stale");
    }
}
