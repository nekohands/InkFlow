using InkFlow.Modules.Billing.Domain;

namespace InkFlow.Modules.Billing.Application;

public enum EntitlementOperationStatus
{
    Success = 0,
    InvalidRequest = 1,
    PlanNotFound = 2,
    UserNotFound = 3,
}

public enum QuotaReservationStatus
{
    Reserved = 0,
    Forbidden = 1,
    Exceeded = 2,
    InvalidRequest = 3,
    Unavailable = 4,
}

public sealed record PlanView(
    string Code,
    int Version,
    string Name,
    long MonthlyQuotaUnits,
    string QuotaAlgorithmVersion,
    IReadOnlyList<string> Entitlements);

public sealed record EntitlementView(
    Guid UserId,
    PlanView Plan,
    DateTimeOffset EffectiveAt);

public sealed record QuotaSnapshot(
    Guid UserId,
    string PlanCode,
    int PlanVersion,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long LimitUnits,
    long UsedUnits,
    long RemainingUnits,
    string AlgorithmVersion);

public sealed record EntitlementOperationResult(
    EntitlementOperationStatus Status,
    EntitlementView? Value = null);

public sealed record QuotaReservationRequest(
    Guid UserId,
    Guid ApplicationId,
    Guid ApiKeyId,
    string Operation,
    long Units,
    string TraceId);

public sealed record QuotaReservationResult(
    QuotaReservationStatus Status,
    QuotaSnapshot? Snapshot = null)
{
    public bool IsReserved => Status == QuotaReservationStatus.Reserved;
}

public interface IBillingUserStatusReader
{
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IPlanRepository
{
    Task<IReadOnlyList<PlanDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task<PlanDefinition?> GetAsync(
        string code,
        int version,
        CancellationToken cancellationToken = default);
}

public interface IEntitlementAssignmentRepository
{
    Task<EntitlementAssignment?> GetLatestForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        EntitlementAssignment assignment,
        CancellationToken cancellationToken = default);
}

public interface IEntitlementService
{
    Task<IReadOnlyList<PlanView>> ListPlansAsync(CancellationToken cancellationToken = default);

    Task<EntitlementView?> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<EntitlementOperationResult> AssignAsync(
        Guid actorId,
        Guid userId,
        string? planCode,
        string? reason,
        CancellationToken cancellationToken = default);
}

public interface IQuotaSnapshotCache
{
    Task<QuotaSnapshot?> GetAsync(
        Guid userId,
        DateTimeOffset periodStart,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        QuotaSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid userId, DateTimeOffset periodStart, CancellationToken cancellationToken = default);
}

public interface IQuotaService
{
    Task<QuotaReservationResult> ReserveAsync(
        QuotaReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<QuotaSnapshot?> GetSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
