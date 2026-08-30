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
    /// <summary>保留消息创建时的 JSON 原文，避免 jsonb 读回规范化后无法重建 hash。</summary>
    public string? RawPayload { get; set; }
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
    /// <summary>保留消息接收时的 JSON 原文，避免 jsonb 读回规范化后无法重建 hash。</summary>
    public string? RawPayload { get; set; }
    public string? TraceId { get; set; }
    /// <summary>消息产生时间；旧 Inbox 行为空时由 ReceivedAt 作为兼容回退。</summary>
    public DateTimeOffset? OccurredAt { get; set; }
    /// <summary>下一次允许领取的时间；旧行为空时视为立即可领取。</summary>
    public DateTimeOffset? AvailableAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LockOwner { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    /// <summary>达到消费尝试上限后的终态标记；死信仍保留为消息事实。</summary>
    public DateTimeOffset? DeadLetteredAt { get; set; }
    public string? LastError { get; set; }
}
