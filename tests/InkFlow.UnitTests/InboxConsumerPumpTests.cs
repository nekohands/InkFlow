using InkFlow.BuildingBlocks.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class InboxConsumerPumpTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Pump_Claims_Registered_Message_And_Confirms_It()
    {
        var message = IntegrationMessage.Create(
            "test.pump.registered",
            "{\"value\":7}",
            T0,
            id: Guid.CreateVersion7());
        var store = new InMemoryInboxStore(message);
        var handler = new RecordingHandler(message.MessageType);
        var resolver = new IntegrationMessageHandlerRegistry([handler]);
        var options = Options("pump-test");
        var consumer = new IntegrationMessageConsumer(
            store,
            resolver,
            new FixedTimeProvider(T0),
            options);
        var pump = new InboxConsumerPump(
            store,
            consumer,
            resolver,
            new FixedTimeProvider(T0),
            options);

        var result = await pump.ConsumeOnceAsync().ConfigureAwait(false);

        Assert.AreEqual(1, result.ClaimedCount);
        Assert.AreEqual(1, result.ProcessedCount);
        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(1, handler.CallCount);
        Assert.IsTrue(store.Processed);
        Assert.AreEqual(1, store.ClaimCount);
    }

    [TestMethod]
    public async Task Pump_With_No_Registered_Handler_Does_Not_Claim_Inbox()
    {
        var message = IntegrationMessage.Create(
            "test.pump.unhandled",
            "{\"value\":8}",
            T0,
            id: Guid.CreateVersion7());
        var store = new InMemoryInboxStore(message);
        var resolver = new IntegrationMessageHandlerRegistry([]);
        var options = Options("pump-empty");
        var consumer = new IntegrationMessageConsumer(
            store,
            resolver,
            new FixedTimeProvider(T0),
            options);
        var pump = new InboxConsumerPump(
            store,
            consumer,
            resolver,
            new FixedTimeProvider(T0),
            options);

        var result = await pump.ConsumeOnceAsync().ConfigureAwait(false);

        Assert.AreEqual(0, result.ClaimedCount);
        Assert.AreEqual(0, store.ClaimCount);
        Assert.IsFalse(store.Processed);
    }

    [TestMethod]
    public async Task Pump_Reports_DeadLettered_Handler_Failure()
    {
        var message = IntegrationMessage.Create(
            "test.pump.dead-letter",
            "{\"value\":9}",
            T0,
            id: Guid.CreateVersion7());
        var store = new InMemoryInboxStore(message);
        var handler = new RecordingHandler(message.MessageType) { ThrowOnHandle = true };
        var resolver = new IntegrationMessageHandlerRegistry([handler]);
        var options = Options("pump-dead-letter", maxAttempts: 1);
        var consumer = new IntegrationMessageConsumer(
            store,
            resolver,
            new FixedTimeProvider(T0),
            options);
        var pump = new InboxConsumerPump(
            store,
            consumer,
            resolver,
            new FixedTimeProvider(T0),
            options);

        var result = await pump.ConsumeOnceAsync().ConfigureAwait(false);

        Assert.AreEqual(1, result.ClaimedCount);
        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(1, result.DeadLetteredCount);
        Assert.IsTrue(store.DeadLettered);
        Assert.AreEqual(1, handler.CallCount);
    }

    private static InboxConsumerOptions Options(string owner, int maxAttempts = 5) =>
        new()
        {
            Owner = owner,
            Enabled = true,
            PollInterval = TimeSpan.FromSeconds(1),
            StartupDelay = TimeSpan.Zero,
            LeaseDuration = TimeSpan.FromMinutes(2),
            BatchSize = 10,
            MaxAttempts = maxAttempts,
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingHandler(string messageType) : IIntegrationMessageHandler
    {
        public string MessageType { get; } = messageType;

        public int CallCount { get; private set; }

        public bool ThrowOnHandle { get; init; }

        public Task HandleAsync(
            IntegrationMessage message,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (ThrowOnHandle)
            {
                throw new InvalidOperationException("handler details must not be persisted");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryInboxStore(IntegrationMessage message) : IInboxStore
    {
        private readonly IntegrationMessage _message = message;
        private string? _lockOwner;
        private DateTimeOffset? _lockedUntil;

        public int ClaimCount { get; private set; }

        public bool Processed { get; private set; }

        public bool DeadLettered { get; private set; }

        public Task<IReadOnlyList<InboxMessageRecord>> ClaimBatchAsync(
            string owner,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            int limit,
            IReadOnlyCollection<string> messageTypes,
            CancellationToken cancellationToken = default)
        {
            ClaimCount++;
            if (limit < 1 ||
                Processed ||
                (_lockedUntil is { } lockedUntil && lockedUntil > now) ||
                !messageTypes.Contains(_message.MessageType, StringComparer.Ordinal))
            {
                return Task.FromResult<IReadOnlyList<InboxMessageRecord>>([]);
            }

            _lockOwner = owner;
            _lockedUntil = now + leaseDuration;
            return Task.FromResult<IReadOnlyList<InboxMessageRecord>>(
                [new InboxMessageRecord(_message, 1)]);
        }

        public Task<InboxClaimResult> TryClaimAsync(
            IntegrationMessage message,
            string owner,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkProcessedAsync(
            Guid messageId,
            string owner,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(_lockOwner, owner);
            Processed = true;
            _lockOwner = null;
            _lockedUntil = null;
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid messageId,
            string owner,
            DateTimeOffset now,
            string failureCode,
            DateTimeOffset? availableAt,
            bool deadLettered,
            CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(_lockOwner, owner);
            _lockOwner = null;
            _lockedUntil = null;
            DeadLettered = deadLettered;
            return Task.CompletedTask;
        }
    }
}
