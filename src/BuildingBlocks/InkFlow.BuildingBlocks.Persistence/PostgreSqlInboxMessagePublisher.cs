using InkFlow.BuildingBlocks.Messaging;

namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>
/// 将 Outbox 记录可靠转入同一 PostgreSQL 事实库中的 Inbox。
/// 这是当前 v1 的内部传输适配器，依靠 Inbox 主键和消息身份摘要抵抗重复投递与篡改。
/// </summary>
public sealed class PostgreSqlInboxMessagePublisher(
    IInboxTransportStore inbox,
    TimeProvider clock) : IIntegrationMessagePublisher
{
    public async Task PublishAsync(
        OutboxMessageRecord message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(clock);

        var envelope = Rehydrate(message);
        await inbox.EnqueueAsync(
                envelope,
                clock.GetUtcNow().ToUniversalTime(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static IntegrationMessage Rehydrate(OutboxMessageRecord message)
    {
        IntegrationMessage envelope;
        try
        {
            envelope = message.RawPayload is null
                ? IntegrationMessage.Restore(
                    message.MessageType,
                    message.Payload,
                    message.OccurredAt,
                    message.PayloadHash,
                    message.TraceId,
                    message.Id)
                : IntegrationMessage.Create(
                    message.MessageType,
                    message.RawPayload,
                    message.OccurredAt,
                    message.TraceId,
                    message.Id);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("outbox message identity is invalid.");
        }

        if (!string.Equals(message.MessageType, envelope.MessageType, StringComparison.Ordinal) ||
            !string.Equals(message.TraceId, envelope.TraceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("outbox message identity is invalid.");
        }

        if (message.RawPayload is not null &&
            !string.Equals(message.PayloadHash, envelope.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("outbox message identity is invalid.");
        }

        return envelope;
    }
}
