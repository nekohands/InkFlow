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
                    [new RuleField("title", new RuleSelector(SelectorKind.Css, ".t"), null, [])]),
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
}
