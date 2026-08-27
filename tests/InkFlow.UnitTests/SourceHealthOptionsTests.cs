using InkFlow.BuildingBlocks.Application;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

/// <summary>
/// 冷却参数配置化：SourceHealthOptions 解析/验证、静态策略装载、
/// 进程内服务实例的半开节奏随配置变化。
/// 注意：测试程序集未开启并行化，静态 Configure 的装载/还原
/// （try/finally 保证）不会污染其他测试。
/// </summary>
[TestClass]
public sealed class SourceHealthOptionsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Default_Options_Match_The_V1_Policy_Curve()
    {
        var options = SourceHealthOptions.Default;

        Assert.AreEqual(
            SourceHealthPolicy.UnhealthyAfterConsecutiveFailures,
            options.UnhealthyAfterConsecutiveFailures);
        Assert.AreEqual(
            SourceHealthPolicy.ProbeCooldownBaseMinutes,
            options.ProbeCooldownBaseMinutes);
        Assert.AreEqual(
            SourceHealthPolicy.ProbeCooldownMaxMinutes,
            options.ProbeCooldownMaxMinutes);

        Assert.AreEqual(TimeSpan.FromMinutes(30), options.ToParameters().ProbeCooldown(3));
        Assert.AreEqual(TimeSpan.FromMinutes(60), options.ToParameters().ProbeCooldown(4));
        Assert.AreEqual(TimeSpan.FromDays(1), options.ToParameters().ProbeCooldown(99));
    }

    [TestMethod]
    public void FromConfiguration_Reads_Keys_And_Falls_Back_To_Defaults()
    {
        // 空配置：全部回退 v1 默认。
        var empty = SourceHealthOptions.FromConfiguration(new ConfigurationBuilder().Build());
        Assert.AreEqual(3, empty.UnhealthyAfterConsecutiveFailures);
        Assert.AreEqual(30, empty.ProbeCooldownBaseMinutes);
        Assert.AreEqual(24 * 60, empty.ProbeCooldownMaxMinutes);

        // 显式覆盖：整条曲线随配置重排。
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceHealth:UnhealthyAfterConsecutiveFailures"] = "5",
                ["SourceHealth:ProbeCooldownBaseMinutes"] = "10",
                ["SourceHealth:ProbeCooldownMaxMinutes"] = "720",
            })
            .Build();
        var options = SourceHealthOptions.FromConfiguration(config);

        Assert.AreEqual(5, options.UnhealthyAfterConsecutiveFailures);
        Assert.AreEqual(10, options.ProbeCooldownBaseMinutes);
        Assert.AreEqual(720, options.ProbeCooldownMaxMinutes);
        var parameters = options.ToParameters();
        Assert.AreEqual(TimeSpan.FromMinutes(10), parameters.ProbeCooldown(5));
        Assert.AreEqual(TimeSpan.FromMinutes(20), parameters.ProbeCooldown(6));
        Assert.AreEqual(TimeSpan.FromMinutes(720), parameters.ProbeCooldown(50),
            "封顶随配置生效，不再是一天");
    }

    [TestMethod]
    public void FromConfiguration_Rejects_Non_Integer_And_Out_Of_Range_Values()
    {
        var notInteger = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceHealth:ProbeCooldownBaseMinutes"] = "soon",
            })
            .Build();

        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => SourceHealthOptions.FromConfiguration(notInteger));
        Assert.IsTrue(ex.Message.Contains("must be an integer"));

        var zeroBase = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceHealth:ProbeCooldownBaseMinutes"] = "0",
            })
            .Build();
        Assert.ThrowsExactly<InvalidOperationException>(
            () => SourceHealthOptions.FromConfiguration(zeroBase));

        // 上限小于基础时长：曲线自相矛盾，拒绝装配。
        var inverted = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceHealth:ProbeCooldownBaseMinutes"] = "60",
                ["SourceHealth:ProbeCooldownMaxMinutes"] = "30",
            })
            .Build();
        Assert.ThrowsExactly<InvalidOperationException>(
            () => SourceHealthOptions.FromConfiguration(inverted));
    }

    [TestMethod]
    public void Policy_Configure_Retunes_Readers_And_Null_Restores_V1()
    {
        try
        {
            SourceHealthPolicy.Configure(new SourceHealthParameters(2, 5, 90));

            Assert.AreEqual(2, SourceHealthPolicy.UnhealthyAfterConsecutiveFailures);
            Assert.AreEqual(TimeSpan.FromMinutes(5), SourceHealthPolicy.ProbeCooldown(2));
            Assert.AreEqual(TimeSpan.FromMinutes(10), SourceHealthPolicy.ProbeCooldown(3));
            Assert.AreEqual(TimeSpan.FromMinutes(90), SourceHealthPolicy.ProbeCooldown(99));

            // 静态 IsProbeDue 与实体实例路径都跟随新曲线（边界含相等）。
            Assert.IsFalse(SourceHealthPolicy.IsProbeDue(2, T0, T0.AddMinutes(4)));
            Assert.IsTrue(SourceHealthPolicy.IsProbeDue(2, T0, T0.AddMinutes(5)));

            var health = SourceCapabilityHealth.Create("official-a", SourceCapability.Search, T0);
            health.RecordFailure("upstream-503", T0);
            health.RecordFailure("upstream-503", T0);
            Assert.AreEqual(SourceHealthStatus.Unhealthy, health.Status,
                "阈值配置为 2 后，两次失败即进入 Unhealthy");
            Assert.IsFalse(health.IsAvailable);
            Assert.IsFalse(health.IsProbeDue(T0.AddMinutes(4)));
            Assert.IsTrue(health.IsProbeDue(T0.AddMinutes(5)));
        }
        finally
        {
            SourceHealthPolicy.Configure(null);
        }

        Assert.AreEqual(3, SourceHealthPolicy.UnhealthyAfterConsecutiveFailures);
        Assert.AreEqual(TimeSpan.FromMinutes(30), SourceHealthPolicy.ProbeCooldown(3),
            "null 必须恢复 v1 默认曲线");
    }

    [TestMethod]
    public async Task Host_Process_Retunes_Half_Open_Rhythm_Via_Configure()
    {
        try
        {
            // 进程内模拟宿主装载：2 分钟基础冷却 + 3 次失败阈值（默认）。
            SourceHealthPolicy.Configure(new SourceHealthParameters(3, 2, 120));

            var repository = new InMemoryHealthRepository();
            var clock = new MutableClock(T0);
            var service = new SourceHealthService(repository, clock);

            for (var i = 0; i < 3; i++)
            {
                await service.RecordFailureAsync(
                    "official-a", SourceCapability.Search, "adapter-exception");
            }

            Assert.IsFalse(await service.IsAvailableAsync("official-a", SourceCapability.Search));

            clock.Now = T0.AddMinutes(1);
            Assert.IsFalse(await service.IsAvailableAsync("official-a", SourceCapability.Search),
                "2 分钟冷却未满不放行");

            clock.Now = T0.AddMinutes(2);
            Assert.IsTrue(await service.IsAvailableAsync("official-a", SourceCapability.Search),
                "配置的基础冷却期满即半开放行");

            // 探针失败：冷却翻倍为 4 分钟，锚点刷新到探针时刻。
            await service.RecordFailureAsync("official-a", SourceCapability.Search, "probe-failed");
            clock.Now = T0.AddMinutes(5);
            Assert.IsFalse(await service.IsAvailableAsync("official-a", SourceCapability.Search));
            clock.Now = T0.AddMinutes(6);
            Assert.IsTrue(await service.IsAvailableAsync("official-a", SourceCapability.Search),
                "翻倍后的 4 分钟冷却同样到期放行");
        }
        finally
        {
            SourceHealthPolicy.Configure(null);
        }

        Assert.AreEqual(TimeSpan.FromMinutes(30), SourceHealthPolicy.ProbeCooldown(3));
    }

    private sealed class MutableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public DateTimeOffset Now
        {
            get => _now;
            set => _now = value;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class InMemoryHealthRepository : ISourceHealthRepository
    {
        public List<SourceCapabilityHealth> Store { get; } = [];

        public Task<SourceCapabilityHealth?> GetAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceCapabilityHealth?>(Store.SingleOrDefault(health =>
                health.SourceId == sourceId && health.Capability == capability));

        public Task AddAsync(
            SourceCapabilityHealth health,
            CancellationToken cancellationToken = default)
        {
            Store.Add(health);
            return Task.CompletedTask;
        }

        public Task SaveAsync(
            SourceCapabilityHealth health,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Store.Where(health => health.SourceId == sourceId).ToList());

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListUnhealthyAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Store.Where(health => health.Status == SourceHealthStatus.Unhealthy).ToList());
    }
}
