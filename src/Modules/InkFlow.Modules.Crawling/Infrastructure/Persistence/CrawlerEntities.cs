namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>crawler.tasks 表实体：CrawlerTask 聚合的持久化形态。</summary>
public sealed class CrawlerTaskEntity
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = null!;
    public int Capability { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
    public string? CredentialReferenceId { get; set; }
    public int Status { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>crawler.dead_letters 表实体。</summary>
public sealed class DeadLetterEntity
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string SourceId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public int AttemptCount { get; set; }
    public DateTimeOffset DeadLetteredAt { get; set; }
    public Guid? ReplayTaskId { get; set; }
    public DateTimeOffset? ReplayedAt { get; set; }
    public string? ReplayRequestedBy { get; set; }
    public string? ReplayReason { get; set; }
}
