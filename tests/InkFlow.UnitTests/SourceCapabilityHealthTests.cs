using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceCapabilityHealthTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Consecutive_Failures_Degrade_Then_Block_One_Capability()
    {
        var health = SourceCapabilityHealth.Create("official-a", SourceCapability.Content, T0);

        Assert.AreEqual(SourceHealthStatus.Unknown, health.Status);
        Assert.IsTrue(health.IsAvailable);

        health.RecordFailure(" timeout ", T0.AddMinutes(1));
        Assert.AreEqual(SourceHealthStatus.Degraded, health.Status);
        Assert.AreEqual(1, health.ConsecutiveFailures);
        Assert.IsTrue(health.IsAvailable);
        Assert.AreEqual("timeout", health.LastFailureReason);

        health.RecordFailure("upstream-503", T0.AddMinutes(2));
        Assert.AreEqual(SourceHealthStatus.Degraded, health.Status);
        Assert.IsTrue(health.IsAvailable);

        health.RecordFailure("upstream-503", T0.AddMinutes(3));
        Assert.AreEqual(SourceHealthStatus.Unhealthy, health.Status);
        Assert.IsFalse(health.IsAvailable);
        Assert.AreEqual(3, health.ConsecutiveFailures);
    }

    [TestMethod]
    public void Success_Recovers_And_Resets_The_Failure_Streak()
    {
        var health = SourceCapabilityHealth.Create("official-a", SourceCapability.Toc, T0);
        health.RecordFailure("empty-toc", T0.AddMinutes(1));
        health.RecordFailure("empty-toc", T0.AddMinutes(2));
        health.RecordFailure("empty-toc", T0.AddMinutes(3));

        health.RecordSuccess(T0.AddMinutes(4));

        Assert.AreEqual(SourceHealthStatus.Healthy, health.Status);
        Assert.AreEqual(0, health.ConsecutiveFailures);
        Assert.IsTrue(health.IsAvailable);
        Assert.AreEqual(T0.AddMinutes(4), health.LastSuccessAt);
        Assert.IsNull(health.LastFailureReason);
    }

    [TestMethod]
    public async Task Service_Upserts_Health_And_Supports_Manual_Disable_Enable()
    {
        var repository = new InMemoryHealthRepository();
        var service = new SourceHealthService(repository, new FixedClock(T0));

        Assert.IsTrue(await service.IsAvailableAsync("official-a", SourceCapability.Content));

        await service.RecordFailureAsync("official-a", SourceCapability.Content, "adapter-exception");
        await service.RecordFailureAsync("official-a", SourceCapability.Content, "adapter-exception");
        await service.RecordFailureAsync("official-a", SourceCapability.Content, "adapter-exception");

        Assert.IsFalse(await service.IsAvailableAsync("official-a", SourceCapability.Content));
        Assert.AreEqual(1, repository.Store.Count);

        await service.EnableAsync("official-a", SourceCapability.Content);
        Assert.IsTrue(await service.IsAvailableAsync("official-a", SourceCapability.Content));

        await service.DisableAsync("official-a", SourceCapability.Content, "maintenance");
        Assert.IsFalse(await service.IsAvailableAsync("official-a", SourceCapability.Content));
        Assert.AreEqual("maintenance", repository.Store.Single().LastFailureReason);
    }

    [TestMethod]
    public async Task Disabled_Source_Is_Unavailable_Even_Without_A_Capability_Health_Record()
    {
        var source = Source.Create("official-a", "Official A", "https://official-a.example", T0);
        source.Disable(T0.AddMinutes(1));
        var service = new SourceHealthService(
            new InMemoryHealthRepository(),
            new FixedClock(T0),
            new SingleSourceRepository(source));

        Assert.IsFalse(await service.IsAvailableAsync("official-a", SourceCapability.Search));
    }

    [TestMethod]
    public void Probe_Cooldown_Doubles_With_Failure_Depth_And_Caps_At_One_Day()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(30),
            SourceHealthPolicy.ProbeCooldown(3), "首次 Unhealthy 的基础冷却");
        Assert.AreEqual(TimeSpan.FromMinutes(60),
            SourceHealthPolicy.ProbeCooldown(4));
        Assert.AreEqual(TimeSpan.FromMinutes(120),
            SourceHealthPolicy.ProbeCooldown(5));
        Assert.AreEqual(TimeSpan.FromDays(1),
            SourceHealthPolicy.ProbeCooldown(20), "持续失败最多每天重试一次");

        // 边界含相等:冷却期满的那一刻即视为到期。
        var failures = 3;
        var anchor = T0;
        Assert.IsFalse(SourceHealthPolicy.IsProbeDue(failures, anchor, anchor.AddMinutes(29)));
        Assert.IsTrue(SourceHealthPolicy.IsProbeDue(failures, anchor, anchor.AddMinutes(30)));
    }

    [TestMethod]
    public void Unhealthy_Probe_Failure_Renews_Anchor_And_Extends_Cooldown()
    {
        var health = SourceCapabilityHealth.Create("official-a", SourceCapability.Toc, T0);
        health.RecordFailure("upstream-503", T0);
        health.RecordFailure("upstream-503", T0);
        health.RecordFailure("upstream-503", T0);

        Assert.AreEqual(SourceHealthStatus.Unhealthy, health.Status);
        Assert.IsFalse(health.IsProbeDue(T0), "刚进入 Unhealthy 不应立刻重探");

        // 探针到期并再次失败:锚点刷新、计数增长、冷却翻倍——自动恢复不会被误报成功。
        health.RecordFailure("upstream-503", T0.AddMinutes(30));

        Assert.AreEqual(4, health.ConsecutiveFailures);
        Assert.AreEqual(SourceHealthStatus.Unhealthy, health.Status);
        Assert.AreEqual(T0.AddMinutes(30), health.UpdatedAt);
        Assert.IsFalse(health.IsProbeDue(T0.AddMinutes(30).AddMinutes(59)));
        Assert.IsTrue(health.IsProbeDue(T0.AddMinutes(30).AddMinutes(60)),
            "第 4 次失败的冷却应翻倍到 60 分钟");
    }

    [TestMethod]
    public void Consecutive_Failure_Count_Grows_Beyond_The_Unhealthy_Threshold()
    {
        var health = SourceCapabilityHealth.Create("official-a", SourceCapability.Content, T0);
        for (var i = 0; i < 7; i++)
        {
            health.RecordFailure("upstream-503", T0.AddMinutes(i));
        }

        Assert.AreEqual(7, health.ConsecutiveFailures, "失败深度是自适应退避的依据,不得封顶");
        Assert.AreEqual(SourceHealthStatus.Unhealthy, health.Status);
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

    [TestMethod]
    public async Task Service_Half_Opens_After_Cooldown_And_Recovers_On_Success()
    {
        var repository = new InMemoryHealthRepository();
        var clock = new MutableClock(T0);
        var service = new SourceHealthService(repository, clock);

        await service.RecordFailureAsync("official-a", SourceCapability.Search, "adapter-exception");
        await service.RecordFailureAsync("official-a", SourceCapability.Search, "adapter-exception");
        await service.RecordFailureAsync("official-a", SourceCapability.Search, "adapter-exception");

        // 失败在 T0..T0+2m 记录,第三次之后 UpdatedAt=T0+2m。
        clock.Now = T0.AddMinutes(10);
        Assert.IsFalse(await service.IsAvailableAsync("official-a", SourceCapability.Search),
            "冷却期内保持不可用");

        clock.Now = T0.AddMinutes(32);
        Assert.IsTrue(await service.IsAvailableAsync("official-a", SourceCapability.Search),
            "30 分钟冷却期满后放行一次探针尝试");

        // 探针仍失败:冷却翻倍到 60 分钟(锚点已刷新到探针时刻)。
        await service.RecordFailureAsync("official-a", SourceCapability.Search, "probe-failed");
        clock.Now = T0.AddMinutes(40);
        Assert.IsFalse(await service.IsAvailableAsync("official-a", SourceCapability.Search),
            "探针失败后立即回到不可用且冷却延长");

        clock.Now = T0.AddMinutes(92);
        Assert.IsTrue(await service.IsAvailableAsync("official-a", SourceCapability.Search),
            "二次冷却(60 分钟)同样会到期放行");

        // 探针成功:直接回到 Healthy 并重置失败链。
        await service.RecordSuccessAsync("official-a", SourceCapability.Search);
        Assert.IsTrue(await service.IsAvailableAsync("official-a", SourceCapability.Search));
        var health = await service.GetAsync("official-a", SourceCapability.Search);
        Assert.IsNotNull(health);
        Assert.AreEqual(SourceHealthStatus.Healthy, health!.Status);
        Assert.AreEqual(0, health.ConsecutiveFailures);
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

        public Task<SourceCapabilityHealth> MutateAsync(
            string sourceId,
            SourceCapability capability,
            SourceHealthMutationKind mutation,
            string? reason,
            DateTimeOffset occurredAt,
            CancellationToken cancellationToken = default)
        {
            var health = Store.SingleOrDefault(existing =>
                existing.SourceId == sourceId && existing.Capability == capability)
                ?? SourceCapabilityHealth.Create(sourceId, capability, occurredAt);

            if (!Store.Contains(health))
            {
                Store.Add(health);
            }

            ApplyMutation(health, mutation, reason, occurredAt);
            return Task.FromResult(health);
        }

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Store.Where(health => health.SourceId == sourceId).ToList());

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListUnhealthyAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Store.Where(health => health.Status == SourceHealthStatus.Unhealthy).ToList());

        private static void ApplyMutation(
            SourceCapabilityHealth health,
            SourceHealthMutationKind mutation,
            string? reason,
            DateTimeOffset occurredAt)
        {
            switch (mutation)
            {
                case SourceHealthMutationKind.RecordSuccess:
                    health.RecordSuccess(occurredAt);
                    break;
                case SourceHealthMutationKind.RecordFailure:
                    health.RecordFailure(reason ?? string.Empty, occurredAt);
                    break;
                case SourceHealthMutationKind.Disable:
                    health.Disable(reason ?? string.Empty, occurredAt);
                    break;
                case SourceHealthMutationKind.Enable:
                    health.Enable(occurredAt);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
        }
    }

    private sealed class SingleSourceRepository(Source source) : ISourceRepository
    {
        public Task AddAsync(Source value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Source?> GetAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Source?>(source.Id == sourceId ? source : null);

        public Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Source>>([source]);

        public Task SaveAsync(Source value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [TestMethod]
    public async Task Service_Uses_Atomic_Mutation_Seam_To_Preserve_Concurrent_Failures()
    {
        var repository = new AtomicOnlyHealthRepository();
        var service = new SourceHealthService(repository, new FixedClock(T0));

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            service.RecordFailureAsync(
                "official-a",
                SourceCapability.Content,
                "upstream-503")));

        Assert.AreEqual(8, repository.MutationCallCount);
        Assert.AreEqual(8, repository.Current!.ConsecutiveFailures);
        Assert.AreEqual(SourceHealthStatus.Unhealthy, repository.Current.Status);
    }

    private sealed class AtomicOnlyHealthRepository : ISourceHealthRepository
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public int MutationCallCount { get; private set; }
        public SourceCapabilityHealth? Current { get; private set; }

        public Task<SourceCapabilityHealth?> GetAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("health mutations must use the atomic repository seam");

        public Task AddAsync(
            SourceCapabilityHealth health,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("health mutations must use the atomic repository seam");

        public Task SaveAsync(
            SourceCapabilityHealth health,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("health mutations must use the atomic repository seam");

        public async Task<SourceCapabilityHealth> MutateAsync(
            string sourceId,
            SourceCapability capability,
            SourceHealthMutationKind mutation,
            string? reason,
            DateTimeOffset occurredAt,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                MutationCallCount++;
                Current ??= SourceCapabilityHealth.Create(sourceId, capability, occurredAt);

                switch (mutation)
                {
                    case SourceHealthMutationKind.RecordSuccess:
                        Current.RecordSuccess(occurredAt);
                        break;
                    case SourceHealthMutationKind.RecordFailure:
                        Current.RecordFailure(reason ?? string.Empty, occurredAt);
                        break;
                    case SourceHealthMutationKind.Disable:
                        Current.Disable(reason ?? string.Empty, occurredAt);
                        break;
                    case SourceHealthMutationKind.Enable:
                        Current.Enable(occurredAt);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
                }

                return Current;
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Current is not null && Current.SourceId == sourceId ? [Current] : []);

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListUnhealthyAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Current?.Status == SourceHealthStatus.Unhealthy ? [Current] : []);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
