namespace InkFlow.BuildingBlocks.Messaging;

public sealed record OutboxMessageRecord(
    Guid Id,
    string MessageType,
    DateTimeOffset OccurredAt,
    DateTimeOffset AvailableAt,
    string Payload,
    string PayloadHash,
    string? TraceId,
    int AttemptCount,
    string? LockOwner,
    DateTimeOffset? LockedUntil,
    DateTimeOffset? ProcessedAt,
    string? LastError);

public enum InboxClaimStatus
{
    Claimed,
    AlreadyProcessed,
    AlreadyInProgress,
}

public sealed record InboxClaimResult(
    Guid MessageId,
    InboxClaimStatus Status,
    int AttemptCount);

/// <summary>
/// PostgreSQL-backed Outbox 端口。实现必须以消息 ID 幂等入队，并以 lease + SKIP LOCKED
/// 支持多个 dispatcher 的 at-least-once 投递。
/// </summary>
public interface IOutboxStore
{
    Task EnqueueAsync(
        IntegrationMessage message,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessageRecord>> ClaimBatchAsync(
        string owner,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        int limit,
        CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        DateTimeOffset availableAt,
        string failureCode,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Inbox 端口。先 claim、成功后 mark processed；处理器崩溃时 lease 到期后可再次领取。
/// </summary>
public interface IInboxStore
{
    Task<InboxClaimResult> TryClaimAsync(
        IntegrationMessage message,
        string owner,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        string failureCode,
        CancellationToken cancellationToken = default);
}
