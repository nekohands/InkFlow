using InkFlow.BuildingBlocks.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class AuditRetentionTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Cleanup_Uses_Cutoff_And_Drains_At_Most_Configured_Batches()
    {
        var store = new FakeRetentionStore(2, 1);
        var service = new AuditRetentionService(store, new FixedTimeProvider(T0));
        var options = new AuditRetentionOptions
        {
            RetentionDays = 30,
            BatchSize = 2,
            MaxBatchesPerRun = 10,
        };

        var result = await service.CleanupAsync(options).ConfigureAwait(false);

        Assert.AreEqual(3, result.DeletedCount);
        Assert.AreEqual(2, store.Calls.Count);
        Assert.AreEqual(T0.AddDays(-30), store.Calls[0].Cutoff);
        Assert.AreEqual(2, store.Calls[0].BatchSize);
    }

    [TestMethod]
    public async Task Cleanup_Stops_At_Maximum_Batch_Count()
    {
        var store = new FakeRetentionStore(2, 2, 2);
        var service = new AuditRetentionService(store, new FixedTimeProvider(T0));
        var options = new AuditRetentionOptions
        {
            BatchSize = 2,
            MaxBatchesPerRun = 2,
        };

        var result = await service.CleanupAsync(options).ConfigureAwait(false);

        Assert.AreEqual(4, result.DeletedCount);
        Assert.AreEqual(2, store.Calls.Count);
    }

    [TestMethod]
    public void FromConfiguration_Reads_And_Validates_Retention_Settings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Audit:Retention:RetentionDays"] = "730",
                ["Audit:Retention:BatchSize"] = "25",
                ["Audit:Retention:MaxBatchesPerRun"] = "4",
            })
            .Build();

        var options = AuditRetentionOptions.FromConfiguration(configuration);

        Assert.AreEqual(730, options.RetentionDays);
        Assert.AreEqual(25, options.BatchSize);
        Assert.AreEqual(4, options.MaxBatchesPerRun);
        Assert.AreEqual(TimeSpan.FromDays(730), options.Retention);
    }

    [TestMethod]
    public void Options_Reject_Unsafe_Retention_Values()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new AuditRetentionOptions { RetentionDays = 0 }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new AuditRetentionOptions { BatchSize = 1_001 }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new AuditRetentionOptions { MaxBatchesPerRun = 0 }.Validate());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRetentionStore(params int[] results) : IAuditRetentionStore
    {
        private readonly Queue<int> _results = new(results);

        public List<RetentionCall> Calls { get; } = [];

        public Task<int> DeleteExpiredBatchAsync(
            DateTimeOffset cutoff,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new(cutoff, batchSize));
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : 0);
        }
    }

    private sealed record RetentionCall(DateTimeOffset Cutoff, int BatchSize);
}
