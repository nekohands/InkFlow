using InkFlow.BuildingBlocks.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class MessagingExecutionTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Outbox_Dispatcher_Publishes_And_Acknowledges()
    {
        var message = CreateMessage("test.dispatch.success");
        var store = new FakeOutboxStore(message);
        var publisher = new RecordingPublisher();
        var dispatcher = CreateDispatcher(store, publisher);

        var result = await dispatcher.DispatchOnceAsync().ConfigureAwait(false);

        Assert.AreEqual(1, result.ClaimedCount);
        Assert.AreEqual(1, result.PublishedCount);
        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(message.Id, publisher.Published.Single().Id);
        Assert.AreEqual(message.Id, store.PublishedIds.Single());
    }

    [TestMethod]
    public async Task Outbox_Dispatcher_Records_Stable_Failure_And_Bounded_Retry()
    {
        var message = CreateMessage("test.dispatch.failure");
        var store = new FakeOutboxStore(message);
        var publisher = new RecordingPublisher { ThrowOnPublish = true };
        var dispatcher = CreateDispatcher(
            store,
            publisher,
            new ExponentialMessageRetryPolicy(
                baseDelay: TimeSpan.FromSeconds(3),
                maxDelay: TimeSpan.FromMinutes(1)));

        var result = await dispatcher.DispatchOnceAsync().ConfigureAwait(false);

        Assert.AreEqual(1, result.ClaimedCount);
        Assert.AreEqual(0, result.PublishedCount);
        Assert.AreEqual(1, result.FailedCount);
        var failure = store.Failures.Single();
        Assert.AreEqual(MessageFailureCodes.PublishFailed, failure.FailureCode);
        Assert.AreEqual(T0.AddSeconds(3), failure.AvailableAt);
        Assert.IsNull(store.Current.LockOwner);
        Assert.IsNull(store.Current.ProcessedAt);
    }

    [TestMethod]
    public async Task Inbox_Consumer_Processes_Once_And_Duplicate_Is_Not_Handled()
    {
        var message = CreateMessage("test.consume.success");
        var store = new FakeInboxStore();
        var handler = new RecordingHandler(message.MessageType);
        var consumer = CreateConsumer(store, handler);

        var first = await consumer.ConsumeAsync(message).ConfigureAwait(false);
        var duplicate = await consumer.ConsumeAsync(message).ConfigureAwait(false);

        Assert.AreEqual(InboxConsumeStatus.Processed, first.Status);
        Assert.AreEqual(InboxConsumeStatus.AlreadyProcessed, duplicate.Status);
        Assert.AreEqual(1, handler.CallCount);
        Assert.AreEqual(1, store.ProcessedIds.Count);
    }

    [TestMethod]
    public async Task Inbox_Consumer_Records_Handler_Failure_Without_Exception_Text()
    {
        var message = CreateMessage("test.consume.failure");
        var store = new FakeInboxStore();
        var handler = new RecordingHandler(message.MessageType) { ThrowOnHandle = true };
        var consumer = CreateConsumer(store, handler);

        var result = await consumer.ConsumeAsync(message).ConfigureAwait(false);

        Assert.AreEqual(InboxConsumeStatus.Failed, result.Status);
        Assert.AreEqual(MessageFailureCodes.HandlerFailed, result.FailureCode);
        Assert.AreEqual(MessageFailureCodes.HandlerFailed, store.Failures.Single().FailureCode);
    }

    [TestMethod]
    public async Task Inbox_Consumer_Releases_Unknown_Message_Type_For_Retry()
    {
        var message = CreateMessage("test.consume.unregistered");
        var store = new FakeInboxStore();
        var consumer = CreateConsumer(store);

        var result = await consumer.ConsumeAsync(message).ConfigureAwait(false);

        Assert.AreEqual(InboxConsumeStatus.NoHandler, result.Status);
        Assert.AreEqual(MessageFailureCodes.HandlerNotRegistered, result.FailureCode);
        Assert.AreEqual(MessageFailureCodes.HandlerNotRegistered, store.Failures.Single().FailureCode);
    }

    [TestMethod]
    public void Handler_Registry_Rejects_Duplicate_Message_Types()
    {
        var first = new RecordingHandler("test.duplicate");
        var second = new RecordingHandler("test.duplicate");

        Assert.Throws<InvalidOperationException>(() =>
            new IntegrationMessageHandlerRegistry([first, second]));
    }

    [TestMethod]
    public void Exponential_Retry_Policy_Is_Bounded()
    {
        var policy = new ExponentialMessageRetryPolicy(
            baseDelay: TimeSpan.FromSeconds(3),
            maxDelay: TimeSpan.FromSeconds(10));

        Assert.AreEqual(TimeSpan.FromSeconds(3), policy.DelayFor(1));
        Assert.AreEqual(TimeSpan.FromSeconds(6), policy.DelayFor(2));
        Assert.AreEqual(TimeSpan.FromSeconds(10), policy.DelayFor(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.DelayFor(0));
    }

    private static IntegrationMessage CreateMessage(string messageType) =>
        IntegrationMessage.Create(messageType, "{\"value\":1}", T0, id: Guid.CreateVersion7());

    private static OutboxDispatcher CreateDispatcher(
        FakeOutboxStore store,
        RecordingPublisher publisher,
        IMessageRetryPolicy? retryPolicy = null) =>
        new(
            store,
            publisher,
            new FixedTimeProvider(T0),
            new OutboxDispatcherOptions
            {
                Owner = "dispatcher-test",
                LeaseDuration = TimeSpan.FromMinutes(2),
                BatchSize = 10,
                RetryPolicy = retryPolicy ?? new ExponentialMessageRetryPolicy(),
            });

    private static IntegrationMessageConsumer CreateConsumer(
        FakeInboxStore store,
        params IIntegrationMessageHandler[] handlers) =>
        new(
            store,
            new IntegrationMessageHandlerRegistry(handlers),
            new FixedTimeProvider(T0),
            new InboxConsumerOptions
            {
                Owner = "consumer-test",
                LeaseDuration = TimeSpan.FromMinutes(2),
            });

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingPublisher : IIntegrationMessagePublisher
    {
        public List<OutboxMessageRecord> Published { get; } = [];

        public bool ThrowOnPublish { get; init; }

        public Task PublishAsync(
            OutboxMessageRecord message,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException("transport details must not be persisted");
            }

            Published.Add(message);
            return Task.CompletedTask;
        }
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

    private sealed class FakeOutboxStore : IOutboxStore
    {
        private OutboxMessageRecord _current;

        public FakeOutboxStore(IntegrationMessage message)
        {
            _current = new(
                message.Id,
                message.MessageType,
                message.OccurredAt,
                message.OccurredAt,
                message.Payload,
                message.PayloadHash,
                message.TraceId,
                0,
                null,
                null,
                null,
                null);
        }

        public OutboxMessageRecord Current => _current;

        public List<Guid> PublishedIds { get; } = [];

        public List<Failure> Failures { get; } = [];

        public Task EnqueueAsync(
            IntegrationMessage message,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<OutboxMessageRecord>> ClaimBatchAsync(
            string owner,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            int limit,
            CancellationToken cancellationToken = default)
        {
            if (_current.ProcessedAt is not null ||
                (_current.LockedUntil is not null && _current.LockedUntil > now) ||
                _current.AvailableAt > now)
            {
                return Task.FromResult<IReadOnlyList<OutboxMessageRecord>>([]);
            }

            _current = _current with
            {
                AttemptCount = _current.AttemptCount + 1,
                LockOwner = owner,
                LockedUntil = now + leaseDuration,
            };
            return Task.FromResult<IReadOnlyList<OutboxMessageRecord>>([_current]);
        }

        public Task MarkPublishedAsync(
            Guid messageId,
            string owner,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            PublishedIds.Add(messageId);
            _current = _current with
            {
                ProcessedAt = now,
                LockOwner = null,
                LockedUntil = null,
            };
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid messageId,
            string owner,
            DateTimeOffset now,
            DateTimeOffset availableAt,
            string failureCode,
            CancellationToken cancellationToken = default)
        {
            Failures.Add(new(messageId, availableAt, failureCode));
            _current = _current with
            {
                AvailableAt = availableAt,
                LockOwner = null,
                LockedUntil = null,
                LastError = failureCode,
            };
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInboxStore : IInboxStore
    {
        private readonly Dictionary<Guid, InboxState> _messages = [];

        public List<Guid> ProcessedIds { get; } = [];

        public List<Failure> Failures { get; } = [];

        public Task<IReadOnlyList<InboxMessageRecord>> ClaimBatchAsync(
            string owner,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            int limit,
            IReadOnlyCollection<string> messageTypes,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<InboxClaimResult> TryClaimAsync(
            IntegrationMessage message,
            string owner,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            if (!_messages.TryGetValue(message.Id, out var state))
            {
                state = new();
                _messages.Add(message.Id, state);
            }

            if (state.Processed)
            {
                return Task.FromResult(new InboxClaimResult(
                    message.Id,
                    InboxClaimStatus.AlreadyProcessed,
                    state.AttemptCount));
            }

            if (state.LockedUntil > now)
            {
                return Task.FromResult(new InboxClaimResult(
                    message.Id,
                    InboxClaimStatus.AlreadyInProgress,
                    state.AttemptCount));
            }

            state.LockOwner = owner;
            state.LockedUntil = now + leaseDuration;
            state.AttemptCount++;
            return Task.FromResult(new InboxClaimResult(
                message.Id,
                InboxClaimStatus.Claimed,
                state.AttemptCount));
        }

        public Task MarkProcessedAsync(
            Guid messageId,
            string owner,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            _messages[messageId].Processed = true;
            _messages[messageId].LockOwner = null;
            _messages[messageId].LockedUntil = default;
            ProcessedIds.Add(messageId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid messageId,
            string owner,
            DateTimeOffset now,
            string failureCode,
            CancellationToken cancellationToken = default)
        {
            var state = _messages[messageId];
            state.LockOwner = null;
            state.LockedUntil = default;
            Failures.Add(new(messageId, now, failureCode));
            return Task.CompletedTask;
        }

        private sealed class InboxState
        {
            public int AttemptCount { get; set; }

            public bool Processed { get; set; }

            public string? LockOwner { get; set; }

            public DateTimeOffset LockedUntil { get; set; }
        }
    }

    private sealed record Failure(
        Guid MessageId,
        DateTimeOffset AvailableAt,
        string FailureCode);
}
