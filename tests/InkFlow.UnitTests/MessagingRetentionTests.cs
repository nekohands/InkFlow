using InkFlow.BuildingBlocks.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class MessagingRetentionTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Cleanup_Uses_Cutoffs_And_Drains_At_Most_Configured_Batches()
    {
        var store = new FakeRetentionStore(
        [
            new MessageRetentionBatchResult(2, 2),
            new MessageRetentionBatchResult(1, 0),
        ]);
        var service = new MessageRetentionService(store, new FixedTimeProvider(T0));
        var options = new MessageRetentionOptions
        {
            OutboxRetentionDays = 30,
            InboxRetentionDays = 10,
            BatchSize = 2,
            MaxBatchesPerRun = 10,
        };

        var result = await service.CleanupAsync(options).ConfigureAwait(false);

        Assert.AreEqual(3, result.OutboxDeletedCount);
        Assert.AreEqual(2, result.InboxDeletedCount);
        Assert.AreEqual(5, result.TotalDeletedCount);
        Assert.AreEqual(2, store.Calls.Count);
        Assert.AreEqual(T0.AddDays(-30), store.Calls[0].OutboxCutoff);
        Assert.AreEqual(T0.AddDays(-10), store.Calls[0].InboxCutoff);
        Assert.AreEqual(2, store.Calls[0].BatchSize);
    }

    [TestMethod]
    public async Task Cleanup_Stops_At_Maximum_Batch_Count()
    {
        var store = new FakeRetentionStore(
            new MessageRetentionBatchResult(2, 2),
            new MessageRetentionBatchResult(2, 2));
        var service = new MessageRetentionService(store, new FixedTimeProvider(T0));
        var options = new MessageRetentionOptions
        {
            BatchSize = 2,
            MaxBatchesPerRun = 2,
        };

        var result = await service.CleanupAsync(options).ConfigureAwait(false);

        Assert.AreEqual(4, result.OutboxDeletedCount);
        Assert.AreEqual(4, result.InboxDeletedCount);
        Assert.AreEqual(2, store.Calls.Count);
    }

    [TestMethod]
    public void FromConfiguration_Reads_And_Validates_Retention_Settings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:Retention:OutboxRetentionDays"] = "45",
                ["Messaging:Retention:InboxRetentionDays"] = "60",
                ["Messaging:Retention:BatchSize"] = "25",
                ["Messaging:Retention:MaxBatchesPerRun"] = "4",
            })
            .Build();

        var options = MessageRetentionOptions.FromConfiguration(configuration);

        Assert.AreEqual(45, options.OutboxRetentionDays);
        Assert.AreEqual(60, options.InboxRetentionDays);
        Assert.AreEqual(25, options.BatchSize);
        Assert.AreEqual(4, options.MaxBatchesPerRun);
        Assert.AreEqual(TimeSpan.FromDays(45), options.OutboxRetention);
        Assert.AreEqual(TimeSpan.FromDays(60), options.InboxRetention);
    }

    [TestMethod]
    public void Options_Reject_Unsafe_Retention_Values()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MessageRetentionOptions { OutboxRetentionDays = 0 }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MessageRetentionOptions { BatchSize = 1_001 }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new MessageRetentionOptions { MaxBatchesPerRun = 0 }.Validate());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRetentionStore : IMessageRetentionStore
    {
        private readonly Queue<MessageRetentionBatchResult> _results;

        public FakeRetentionStore(params MessageRetentionBatchResult[] results) =>
            _results = new Queue<MessageRetentionBatchResult>(results);

        public List<RetentionCall> Calls { get; } = [];

        public Task<MessageRetentionBatchResult> DeleteProcessedBatchAsync(
            DateTimeOffset outboxCutoff,
            DateTimeOffset inboxCutoff,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new(outboxCutoff, inboxCutoff, batchSize));
            return Task.FromResult(
                _results.Count > 0
                    ? _results.Dequeue()
                    : new MessageRetentionBatchResult(0, 0));
        }
    }

    private sealed record RetentionCall(
        DateTimeOffset OutboxCutoff,
        DateTimeOffset InboxCutoff,
        int BatchSize);
}
