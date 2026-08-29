namespace InkFlow.Modules.Billing.Domain;

public static class CommercialEntitlements
{
    public const string DeveloperCatalogRead = "developer.catalog.read";
}

public static class CommercialPlanCodes
{
    public const string Free = "free";
    public const string Pro = "pro";
    public const string Developer = "developer";
    public const int Version = 1;
    public const string QuotaAlgorithmVersion = "quota-v1";
}

/// <summary>版本化套餐定义；业务检查 Entitlement，不直接检查套餐名称。</summary>
public sealed class PlanDefinition
{
    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 128;
    public const int MaxEntitlementLength = 128;

    public string Code { get; private set; } = null!;
    public int Version { get; private set; }
    public string Name { get; private set; } = null!;
    public long MonthlyQuotaUnits { get; private set; }
    public string QuotaAlgorithmVersion { get; private set; } = null!;
    public IReadOnlyList<string> Entitlements { get; private set; } = [];

    private PlanDefinition() { }

    public static PlanDefinition Create(
        string code,
        int version,
        string name,
        long monthlyQuotaUnits,
        string quotaAlgorithmVersion,
        IEnumerable<string> entitlements)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > MaxCodeLength ||
            code.Any(char.IsWhiteSpace) || code.Any(char.IsControl))
        {
            throw new ArgumentException("plan code is invalid.", nameof(code));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength ||
            name.Any(char.IsControl))
        {
            throw new ArgumentException("plan name is invalid.", nameof(name));
        }

        if (monthlyQuotaUnits < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyQuotaUnits));
        }

        if (string.IsNullOrWhiteSpace(quotaAlgorithmVersion) ||
            quotaAlgorithmVersion.Length > MaxCodeLength ||
            quotaAlgorithmVersion.Any(char.IsWhiteSpace) ||
            quotaAlgorithmVersion.Any(char.IsControl))
        {
            throw new ArgumentException("quota algorithm version is invalid.", nameof(quotaAlgorithmVersion));
        }

        var normalizedEntitlements = entitlements
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedEntitlements.Length == 0 ||
            normalizedEntitlements.Any(value => value.Length > MaxEntitlementLength || value.Any(char.IsControl)))
        {
            throw new ArgumentException("plan entitlements are invalid.", nameof(entitlements));
        }

        return new PlanDefinition
        {
            Code = code.Trim().ToLowerInvariant(),
            Version = version,
            Name = name.Trim(),
            MonthlyQuotaUnits = monthlyQuotaUnits,
            QuotaAlgorithmVersion = quotaAlgorithmVersion.Trim(),
            Entitlements = normalizedEntitlements,
        };
    }

    public static PlanDefinition Rehydrate(
        string code,
        int version,
        string name,
        long monthlyQuotaUnits,
        string quotaAlgorithmVersion,
        IEnumerable<string> entitlements) =>
        Create(code, version, name, monthlyQuotaUnits, quotaAlgorithmVersion, entitlements);

    public bool Grants(string entitlement) =>
        Entitlements.Contains(entitlement, StringComparer.Ordinal);
}

public static class BuiltInPlans
{
    public static IReadOnlyList<PlanDefinition> All { get; } =
    [
        PlanDefinition.Create(
            CommercialPlanCodes.Free,
            CommercialPlanCodes.Version,
            "Free",
            monthlyQuotaUnits: 1_000,
            CommercialPlanCodes.QuotaAlgorithmVersion,
            [CommercialEntitlements.DeveloperCatalogRead]),
        PlanDefinition.Create(
            CommercialPlanCodes.Pro,
            CommercialPlanCodes.Version,
            "Pro",
            monthlyQuotaUnits: 100_000,
            CommercialPlanCodes.QuotaAlgorithmVersion,
            [CommercialEntitlements.DeveloperCatalogRead]),
        PlanDefinition.Create(
            CommercialPlanCodes.Developer,
            CommercialPlanCodes.Version,
            "Developer",
            monthlyQuotaUnits: 1_000_000,
            CommercialPlanCodes.QuotaAlgorithmVersion,
            [CommercialEntitlements.DeveloperCatalogRead]),
    ];
}

/// <summary>用户套餐切换的不可变历史记录；当前套餐由最新记录派生。</summary>
public sealed class EntitlementAssignment
{
    public const int MaxReasonLength = 512;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string PlanCode { get; private set; } = null!;
    public int PlanVersion { get; private set; }
    public Guid AssignedBy { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private EntitlementAssignment() { }

    public static EntitlementAssignment Create(
        Guid userId,
        string planCode,
        int planVersion,
        Guid assignedBy,
        string reason,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty || assignedBy == Guid.Empty)
        {
            throw new ArgumentException("user and actor IDs must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(planCode) || planCode.Any(char.IsWhiteSpace) ||
            planCode.Any(char.IsControl))
        {
            throw new ArgumentException("plan code is invalid.", nameof(planCode));
        }

        if (planVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(planVersion));
        }

        var normalizedReason = NormalizeReason(reason);
        return new EntitlementAssignment
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            PlanCode = planCode.Trim().ToLowerInvariant(),
            PlanVersion = planVersion,
            AssignedBy = assignedBy,
            Reason = normalizedReason,
            CreatedAt = createdAt,
        };
    }

    public static EntitlementAssignment Rehydrate(
        Guid id,
        Guid userId,
        string planCode,
        int planVersion,
        Guid assignedBy,
        string reason,
        DateTimeOffset createdAt) => new()
        {
            Id = id,
            UserId = userId,
            PlanCode = planCode,
            PlanVersion = planVersion,
            AssignedBy = assignedBy,
            Reason = reason,
            CreatedAt = createdAt,
        };

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("reason must not be empty.", nameof(reason));
        }

        var normalized = reason.Trim().Replace('\r', ' ').Replace('\n', ' ');
        if (normalized.Length == 0 || normalized.Length > MaxReasonLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return normalized;
    }
}

/// <summary>一次已准入 API 操作的不可变配额事实。</summary>
public sealed class UsageLedgerEntry
{
    public const int MaxOperationLength = 128;
    public const int MaxAlgorithmVersionLength = 64;
    public const int MaxTraceIdLength = 128;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public Guid ApiKeyId { get; private set; }
    public DateTimeOffset PeriodStart { get; private set; }
    public string Operation { get; private set; } = null!;
    public long Units { get; private set; }
    public string AlgorithmVersion { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public string TraceId { get; private set; } = null!;

    private UsageLedgerEntry() { }

    public static UsageLedgerEntry Create(
        Guid userId,
        Guid applicationId,
        Guid apiKeyId,
        DateTimeOffset periodStart,
        string operation,
        long units,
        string algorithmVersion,
        DateTimeOffset occurredAt,
        string traceId)
    {
        if (userId == Guid.Empty || applicationId == Guid.Empty || apiKeyId == Guid.Empty)
        {
            throw new ArgumentException("usage identities must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(operation) || operation.Length > MaxOperationLength ||
            operation.Any(char.IsWhiteSpace) || operation.Any(char.IsControl))
        {
            throw new ArgumentException("operation is invalid.", nameof(operation));
        }

        if (units < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(units));
        }

        if (string.IsNullOrWhiteSpace(algorithmVersion) || algorithmVersion.Length > MaxAlgorithmVersionLength ||
            algorithmVersion.Any(char.IsWhiteSpace) || algorithmVersion.Any(char.IsControl))
        {
            throw new ArgumentException("algorithmVersion is invalid.", nameof(algorithmVersion));
        }

        if (string.IsNullOrWhiteSpace(traceId) || traceId.Length > MaxTraceIdLength || traceId.Any(char.IsControl))
        {
            throw new ArgumentException("traceId is invalid.", nameof(traceId));
        }

        return new UsageLedgerEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ApplicationId = applicationId,
            ApiKeyId = apiKeyId,
            PeriodStart = periodStart,
            Operation = operation.Trim(),
            Units = units,
            AlgorithmVersion = algorithmVersion.Trim(),
            OccurredAt = occurredAt,
            TraceId = traceId.Trim(),
        };
    }
}
