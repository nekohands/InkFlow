using InkFlow.BuildingBlocks.Messaging;
using InkFlow.BuildingBlocks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class MessagingRelayTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 30, 6, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task PostgreSqlInboxPublisher_Preserves_Envelope_And_Uses_Receipt_Time()
    {
        var message = IntegrationMessage.Create(
            "test.relay.publish",
            "{\"value\":42}",
            T0,
            traceId: "trace-relay-42",
            id: Guid.CreateVersion7());
        var inbox = new RecordingInboxTransportStore();
        var publisher = new PostgreSqlInboxMessagePublisher(
            inbox,
            new FixedTimeProvider(T0.AddSeconds(3)));

        await publisher.PublishAsync(ToRecord(message)).ConfigureAwait(false);

        var received = inbox.Messages.Single();
        Assert.AreEqual(message, received.Message);
        Assert.AreEqual(T0.AddSeconds(3), received.ReceivedAt);
    }

    [TestMethod]
    public async Task PostgreSqlInboxPublisher_Rejects_Tampered_Payload_Hash_Before_Storage()
    {
        var message = IntegrationMessage.Create(
            "test.relay.tampered",
            "{\"value\":43}",
            T0,
            id: Guid.CreateVersion7());
        var inbox = new RecordingInboxTransportStore();
        var publisher = new PostgreSqlInboxMessagePublisher(
            inbox,
            new FixedTimeProvider(T0));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(
                ToRecord(message) with
                {
                    PayloadHash = new string('a', 64),
                })).ConfigureAwait(false);

        Assert.AreEqual("outbox message identity is invalid.", exception.Message);
        Assert.AreEqual(0, inbox.Messages.Count);
    }

    [TestMethod]
    public void RelayOptions_Read_And_Create_Bounded_Dispatcher_Settings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:Relay:Enabled"] = "false",
                ["Messaging:Relay:OwnerPrefix"] = "test-relay",
                ["Messaging:Relay:PollInterval"] = "00:00:02",
                ["Messaging:Relay:StartupDelay"] = "00:00:00",
                ["Messaging:Relay:LeaseDuration"] = "00:05:00",
                ["Messaging:Relay:BatchSize"] = "12",
            })
            .Build();

        var options = OutboxRelayOptions.FromConfiguration(configuration);
        var dispatcherOptions = options.CreateDispatcherOptions("test-owner");

        Assert.IsFalse(options.Enabled);
        Assert.AreEqual("test-relay", options.OwnerPrefix);
        Assert.AreEqual(TimeSpan.FromSeconds(2), options.PollInterval);
        Assert.AreEqual(TimeSpan.Zero, options.StartupDelay);
        Assert.AreEqual(TimeSpan.FromMinutes(5), options.LeaseDuration);
        Assert.AreEqual(12, options.BatchSize);
        Assert.AreEqual("test-owner", dispatcherOptions.Owner);
        Assert.AreEqual(options.LeaseDuration, dispatcherOptions.LeaseDuration);
        Assert.AreEqual(options.BatchSize, dispatcherOptions.BatchSize);
    }

    [TestMethod]
    public void RelayOptions_Reject_Unbounded_Or_Invalid_Settings()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new OutboxRelayOptions { OwnerPrefix = "\u0001" }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new OutboxRelayOptions { PollInterval = TimeSpan.FromMilliseconds(1) }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new OutboxRelayOptions { LeaseDuration = TimeSpan.Zero }.Validate());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new OutboxRelayOptions { BatchSize = 101 }.Validate());
    }

    private static OutboxMessageRecord ToRecord(IntegrationMessage message) =>
        new(
            message.Id,
            message.MessageType,
            message.OccurredAt,
            message.OccurredAt,
            message.Payload,
            message.PayloadHash,
            message.TraceId,
            1,
            "relay-test",
            T0.AddMinutes(2),
            null,
            null);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingInboxTransportStore : IInboxTransportStore
    {
        public List<ReceivedMessage> Messages { get; } = [];

        public Task EnqueueAsync(
            IntegrationMessage message,
            DateTimeOffset receivedAt,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new(message, receivedAt));
            return Task.CompletedTask;
        }
    }

    private sealed record ReceivedMessage(
        IntegrationMessage Message,
        DateTimeOffset ReceivedAt);
}
