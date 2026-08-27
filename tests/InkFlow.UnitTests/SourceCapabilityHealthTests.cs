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
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
