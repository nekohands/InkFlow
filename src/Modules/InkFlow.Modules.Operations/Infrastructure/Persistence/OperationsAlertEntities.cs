namespace InkFlow.Modules.Operations.Infrastructure.Persistence;

public sealed class OperationsAlertIncidentEntity
{
    public string Fingerprint { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public string ResourceType { get; set; } = null!;

    public string ResourceId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset LastTransitionAt { get; set; }

    public DateTimeOffset? LastResolvedAt { get; set; }

    public int OccurrenceCount { get; set; }
}

public sealed class OperationsAlertHistoryEntity
{
    public Guid Id { get; set; }

    public string Fingerprint { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public string ResourceType { get; set; } = null!;

    public string ResourceId { get; set; } = null!;

    public string Transition { get; set; } = null!;

    public DateTimeOffset OccurredAt { get; set; }

    public int OccurrenceCount { get; set; }
}
