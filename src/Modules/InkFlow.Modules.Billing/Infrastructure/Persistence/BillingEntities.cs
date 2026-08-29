namespace InkFlow.Modules.Billing.Infrastructure.Persistence;

public static class BillingSchema
{
    public const string Name = "billing";
}

public sealed class PlanEntity
{
    public string Code { get; set; } = null!;
    public int Version { get; set; }
    public string Name { get; set; } = null!;
    public long MonthlyQuotaUnits { get; set; }
    public string QuotaAlgorithmVersion { get; set; } = null!;
    public string EntitlementsJson { get; set; } = null!;
}

public sealed class EntitlementAssignmentEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = null!;
    public int PlanVersion { get; set; }
    public Guid AssignedBy { get; set; }
    public string Reason { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class UsagePeriodEntity
{
    public Guid UserId { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public long UsedUnits { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class UsageLedgerEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid ApiKeyId { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public string Operation { get; set; } = null!;
    public long Units { get; set; }
    public string AlgorithmVersion { get; set; } = null!;
    public DateTimeOffset OccurredAt { get; set; }
    public string TraceId { get; set; } = null!;
}
