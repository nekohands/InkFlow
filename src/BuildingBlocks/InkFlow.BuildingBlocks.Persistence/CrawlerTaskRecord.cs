namespace InkFlow.BuildingBlocks.Persistence;

public static class CrawlerTaskStatuses
{
    public const string Pending = "Pending";
    public const string Leased = "Leased";
    public const string Completed = "Completed";
    public const string DeadLetter = "DeadLetter";
}

public sealed class CrawlerTaskRecord
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string Payload { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Status { get; set; } = CrawlerTaskStatuses.Pending;
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset ScheduledAtUtc { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? TraceId { get; set; }
}
