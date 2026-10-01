using InkFlow.Modules.Identity.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class IdentityRetentionTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Cleanup_Uses_Grace_Cutoff_And_Drains_At_Most_Configured_Batches()
    {
        var store = new FakeRetentionStore((2, 3), (1, 0));
        var service = new IdentityRetentionService(store, new FixedTimeProvider(T0));
        var options = new IdentityRetentionOptions
        {
            GraceDays = 7,
            BatchSize = 2,
            MaxBatchesPerRun = 10,
        };

        var result = await service.CleanupAsync(options).ConfigureAwait(false);

        Assert.AreEqual(3, result.DeletedSessions);
        Assert.AreEqual(3, result.DeletedAccessTokens);
        Assert.AreEqual(2, store.Calls.Count);
        Assert.AreEqual(T0.AddDays(-7), store.Calls[0].SessionCutoff);
        Assert.AreEqual(T0.AddDays(-7), store.Calls[0].AccessTokenCutoff);
        Assert.AreEqual(2, store.Calls[0].BatchSize);
    }

    [TestMethod]
    public async Task Cleanup_Stops_At_Maximum_Batch_Count()
    {
        var store = new FakeRetentionStore((5, 5), (5, 5), (5, 5));
        var service = new IdentityRetentionService(store, new FixedTimeProvider(T0));
        var options = new IdentityRetentionOptions
        {
            BatchSize = 5,
            MaxBatchesPerRun = 2,
        };

        var result = await service.CleanupAsync(options).ConfigureAwait(false);

        Assert.AreEqual(10, result.DeletedSessions);
        Assert.AreEqual(10, result.DeletedAccessTokens);
        Assert.AreEqual(2, store.Calls.Count);
    }

    [TestMethod]
    public async Task Cleanup_Stops_Early_When_Both_Collections_Are_Drained()
    {
        var store = new FakeRetentionStore((1, 0));
        var service = new IdentityRetentionService(store, new FixedTimeProvider(T0));
        var options = new IdentityRetentionOptions { BatchSize = 5 };

        var result = await service.CleanupAsync(options).ConfigureAwait(false);

        Assert.AreEqual(1, result.DeletedSessions);
        Assert.AreEqual(0, result.DeletedAccessTokens);
        Assert.AreEqual(1, store.Calls.Count);
    }

    [TestMethod]
    public void FromConfiguration_Reads_And_Validates_Retention_Settings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Retention:GraceDays"] = "14",
                ["Identity:Retention:BatchSize"] = "250",
                ["Identity:Retention:MaxBatchesPerRun"] = "3",
            })
            .Build();

        var options = IdentityRetentionOptions.FromConfiguration(configuration);

        Assert.AreEqual(14, options.GraceDays);
        Assert.AreEqual(250, options.BatchSize);
        Assert.AreEqual(3, options.MaxBatchesPerRun);
        Assert.AreEqual(TimeSpan.FromDays(14), options.Grace);
    }

    [TestMethod]
    public void Options_Reject_Unsafe_Retention_Values()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new IdentityRetentionOptions { GraceDays = -1 }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new IdentityRetentionOptions { GraceDays = 366 }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new IdentityRetentionOptions { BatchSize = 1_001 }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new IdentityRetentionOptions { MaxBatchesPerRun = 0 }.Validate());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRetentionStore(
        params (int Sessions, int AccessTokens)[] results) : IIdentityRetentionStore
    {
        private readonly Queue<(int Sessions, int AccessTokens)> _results = new(results);

        public List<RetentionCall> Calls { get; } = [];

        public Task<IdentityRetentionResult> DeleteExpiredBatchAsync(
            DateTimeOffset sessionCutoff,
            DateTimeOffset accessTokenCutoff,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new(sessionCutoff, accessTokenCutoff, batchSize));
            var next = _results.Count > 0
                ? _results.Dequeue()
                : (Sessions: 0, AccessTokens: 0);
            return Task.FromResult(new IdentityRetentionResult(next.Sessions, next.AccessTokens));
        }
    }

    private sealed record RetentionCall(
        DateTimeOffset SessionCutoff,
        DateTimeOffset AccessTokenCutoff,
        int BatchSize);
}
