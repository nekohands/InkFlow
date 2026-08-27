using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>Sources 仓储集成测试：真实 PostgreSQL 18 上验证迁移与规则文档 jsonb 往返。</summary>
[TestClass]
public sealed class SourceRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 18, 0, 0, TimeSpan.Zero);

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

    private static EfSourceRepository CreateRepository()
    {
        var options = new DbContextOptionsBuilder<SourcesDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new SourcesDbContext(options);
        db.Database.Migrate();
        return new EfSourceRepository(db);
    }

    private static EfSourceHealthRepository CreateHealthRepository()
    {
        var options = new DbContextOptionsBuilder<SourcesDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new SourcesDbContext(options);
        db.Database.Migrate();
        return new EfSourceHealthRepository(db);
    }

    private static Source NewSourceWithRules(string sourceId) =>
        Source.Rehydrate(
            sourceId,
            "示例来源",
            "https://books.example.com",
            new SourceRuleDsl("1", sourceId,
            [
                new CapabilityRule(
                    SourceCapability.Search,
                    RuleRequest.Get("/search?q={query}"),
                    [new RuleField("title", new RuleSelector(SelectorKind.Css, "h1"), null, [])]),
            ]),
            T0,
            T0);

    [TestMethod]
    public async Task Source_With_Rule_Dsl_Roundtrips()
    {
        var repo = CreateRepository();
        var source = NewSourceWithRules("roundtrip-source");
        await repo.AddAsync(source).ConfigureAwait(false);

        var loaded = await repo.GetAsync("roundtrip-source").ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("https://books.example.com", loaded.BaseUrl);
        Assert.IsNotNull(loaded.RuleDsl, "jsonb 规则文档应完整往返");
        Assert.IsNotNull(loaded.FindRule(SourceCapability.Search));
    }

    [TestMethod]
    public async Task Save_Updates_Rule_Document()
    {
        var repo = CreateRepository();
        await repo.AddAsync(NewSourceWithRules("save-source")).ConfigureAwait(false);

        var loaded = (await repo.GetAsync("save-source").ConfigureAwait(false))!;
        loaded.UpdateRuleDsl(
            new SourceRuleDsl("1", "save-source",
            [
                new CapabilityRule(
                    SourceCapability.Toc,
                    RuleRequest.Get("/toc/{bookId}"),
                    [],
                    List: new RuleListBinding(
                        ItemsSelector: ".toc a",
                        ExternalIdAttribute: "href",
                        IdPrefixToStrip: string.Empty,
                        IdSuffixToStrip: ".html")),
            ]),
            T0.AddMinutes(1));
        await repo.SaveAsync(loaded).ConfigureAwait(false);

        var reloaded = await repo.GetAsync("save-source").ConfigureAwait(false);
        Assert.IsNull(reloaded!.FindRule(SourceCapability.Search), "规则替换应为整体覆盖");
        Assert.IsNotNull(reloaded.FindRule(SourceCapability.Toc));
    }

    [TestMethod]
    public async Task Missing_Source_Returns_Null()
    {
        var repo = CreateRepository();
        Assert.IsNull(await repo.GetAsync("nope").ConfigureAwait(false));
    }

    [TestMethod]
    public async Task List_Returns_All_Registered_Sources()
    {
        // 共享容器下其他用例可能已有来源行,断言只针对本用例专属 ID 的存在性。
        var repo = CreateRepository();
        await repo.AddAsync(NewSourceWithRules("list-src-a")).ConfigureAwait(false);
        await repo.AddAsync(NewSourceWithRules("list-src-b")).ConfigureAwait(false);

        var all = await repo.ListAsync().ConfigureAwait(false);
        var ids = all.Select(s => s.Id).ToList();

        Assert.IsTrue(ids.Contains("list-src-a"), "List 必须包含新登记的 list-src-a");
        Assert.IsTrue(ids.Contains("list-src-b"), "List 必须包含新登记的 list-src-b");
        Assert.IsTrue(ids.Distinct().Count() == ids.Count, "List 不得返回重复来源");
    }

    [TestMethod]
    public async Task Capability_Health_Roundtrips_Status_And_Evidence()
    {
        var repo = CreateHealthRepository();
        var health = SourceCapabilityHealth.Create(
            "health-roundtrip-source", SourceCapability.Content, T0);
        health.RecordFailure("adapter-exception", T0.AddMinutes(1));

        await repo.AddAsync(health).ConfigureAwait(false);

        var loaded = await repo
            .GetAsync("health-roundtrip-source", SourceCapability.Content)
            .ConfigureAwait(false);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(SourceHealthStatus.Degraded, loaded.Status);
        Assert.AreEqual(1, loaded.ConsecutiveFailures);
        Assert.AreEqual("adapter-exception", loaded.LastFailureReason);
        Assert.AreEqual(SourceHealthPolicy.AlgorithmVersion, loaded.AlgorithmVersion);
    }

    [TestMethod]
    public async Task ListUnhealthy_Returns_Only_Unhealthy_Capability_Rows()
    {
        var repo = CreateHealthRepository();

        var healthy = SourceCapabilityHealth.Create("probe-filter-src", SourceCapability.Toc, T0);
        healthy.RecordSuccess(T0.AddMinutes(1));

        var degraded = SourceCapabilityHealth.Create("probe-filter-src", SourceCapability.BookInfo, T0);
        degraded.RecordFailure("timeout", T0.AddMinutes(1));
        degraded.RecordFailure("timeout", T0.AddMinutes(2));

        var unhealthy = SourceCapabilityHealth.Create("probe-filter-src", SourceCapability.Search, T0);
        unhealthy.RecordFailure("upstream-503", T0.AddMinutes(1));
        unhealthy.RecordFailure("upstream-503", T0.AddMinutes(2));
        unhealthy.RecordFailure("upstream-503", T0.AddMinutes(3));

        // 该来源的 Content 完全没有健康行:未探测过的能力不应出现在巡检候选里。

        foreach (var row in new[] { healthy, degraded, unhealthy })
        {
            await repo.AddAsync(row).ConfigureAwait(false);
        }

        var result = await repo.ListUnhealthyAsync().ConfigureAwait(false);

        var unhealthyRow = result.Single();
        Assert.AreEqual(SourceCapability.Search, unhealthyRow.Capability);
        Assert.AreEqual(SourceHealthStatus.Unhealthy, unhealthyRow.Status);
    }
}
