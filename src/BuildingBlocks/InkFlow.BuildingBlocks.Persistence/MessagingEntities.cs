namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>Outbox 消息事实；已发布行保留到运维保留任务清理。</summary>
public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = null!;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public string Payload { get; set; } = null!;
    public string PayloadHash { get; set; } = null!;
    public string? TraceId { get; set; }
    public int AttemptCount { get; set; }
    public string? LockOwner { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// Inbox 去重事实。Message ID 是主键；消息只有在处理成功后才标记 ProcessedAt，
/// 因而处理器崩溃时可在 lease 到期后再次投递。
/// </summary>
public sealed class InboxMessageEntity
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public string PayloadHash { get; set; } = null!;
    public DateTimeOffset ReceivedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LockOwner { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}
