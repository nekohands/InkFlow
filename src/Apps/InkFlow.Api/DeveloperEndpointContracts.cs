using InkFlow.Modules.Billing.Application;

namespace InkFlow.Api;

public sealed record DeveloperApplicationRequest(string? Name);

public sealed record DeveloperApiKeyRequest(string? Name, int? ExpiresInDays);

public sealed record DeveloperEntitlementRequest(string? PlanCode, string? Reason);

public sealed record DeveloperApplicationResponse(
    Guid ApplicationId,
    Guid UserId,
    string Name,
    string Environment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);

public sealed record DeveloperApiKeyResponse(
    Guid KeyId,
    Guid ApplicationId,
    string Name,
    string Prefix,
    string Scope,
    string Environment,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

public sealed record DeveloperApiKeyIssueResponse(
    DeveloperApiKeyResponse Key,
    string ApiKey);

public sealed record DeveloperPlanResponse(
    string Code,
    int Version,
    string Name,
    long MonthlyQuotaUnits,
    string QuotaAlgorithmVersion,
    IReadOnlyList<string> Entitlements);

public sealed record DeveloperQuotaResponse(
    string PlanCode,
    int PlanVersion,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long LimitUnits,
    long UsedUnits,
    long RemainingUnits,
    string AlgorithmVersion);

public sealed record DeveloperEntitlementResponse(
    Guid UserId,
    DeveloperPlanResponse Plan,
    DateTimeOffset EffectiveAt,
    DeveloperQuotaResponse? Quota);

internal static class DeveloperEntitlementResponses
{
    public static DeveloperPlanResponse ToResponse(PlanView value) =>
        new(
            value.Code,
            value.Version,
            value.Name,
            value.MonthlyQuotaUnits,
            value.QuotaAlgorithmVersion,
            value.Entitlements);

    public static DeveloperEntitlementResponse ToResponse(
        EntitlementView value,
        QuotaSnapshot? snapshot) =>
        new(
            value.UserId,
            ToResponse(value.Plan),
            value.EffectiveAt,
            snapshot is null
                ? null
                : new DeveloperQuotaResponse(
                    snapshot.PlanCode,
                    snapshot.PlanVersion,
                    snapshot.PeriodStart,
                    snapshot.PeriodEnd,
                    snapshot.LimitUnits,
                    snapshot.UsedUnits,
                    snapshot.RemainingUnits,
                    snapshot.AlgorithmVersion));
}
