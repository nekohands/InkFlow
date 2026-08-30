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
    string? LastError,
    /// <summary>JSON 原文；用于跨 jsonb 规范化边界稳定重建 PayloadHash。</summary>
    string? RawPayload = null);

public enum InboxClaimStatus
{
    Claimed,
    AlreadyProcessed,
    AlreadyInProgress,
    RetryScheduled,
    DeadLettered,
}

public sealed record InboxClaimResult(
    Guid MessageId,
    InboxClaimStatus Status,
    int AttemptCount);

/// <summary>
/// 已由 Inbox 持久化层领取、等待 Handler 处理的消息。
/// 领取动作与后续确认分开，允许宿主在数据库租约内执行 Handler。
/// </summary>
public sealed record InboxMessageRecord(
    IntegrationMessage Message,
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
/// 将已领取的 Outbox 消息写入受治理的 Inbox 事实表。
/// 实现必须以消息 ID 幂等入队，并保留第一次写入的接收时间。
/// </summary>
public interface IInboxTransportStore
{
    Task EnqueueAsync(
        IntegrationMessage message,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Inbox 端口。先 claim、成功后 mark processed；处理器失败时由调用方持久化下一次领取时间，
/// 达到尝试上限后保留为终态死信。
/// </summary>
public interface IInboxStore
{
    Task<IReadOnlyList<InboxMessageRecord>> ClaimBatchAsync(
        string owner,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        int limit,
        IReadOnlyCollection<string> messageTypes,
        CancellationToken cancellationToken = default);

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

    /// <param name="availableAt">下一次允许领取的 UTC 时间；死信时必须为空。</param>
    /// <param name="deadLettered">是否将消息标记为终态死信；死信不会再被批量领取。</param>
    Task MarkFailedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        string failureCode,
        DateTimeOffset? availableAt,
        bool deadLettered,
        CancellationToken cancellationToken = default);
}
