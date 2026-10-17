using InkFlow.Modules.Billing.Infrastructure.Persistence;

namespace InkFlow.Modules.Billing.Application;

/// <summary>
/// 额度应用门面：保留 API/DI 入口，同时把预留写入与快照查询交给各自用例。
/// </summary>
public sealed class QuotaService : IQuotaService
{
    private readonly QuotaReservationUseCase reservations;
    private readonly QuotaSnapshotQuery snapshotQuery;

    public QuotaService(
        BillingDbContext db,
        IEntitlementService entitlements,
        IQuotaSnapshotCache cache,
        TimeProvider clock)
    {
        reservations = new(db, entitlements, cache, clock);
        snapshotQuery = new(db, entitlements, cache, clock);
    }

    public Task<QuotaReservationResult> ReserveAsync(
        QuotaReservationRequest request,
        CancellationToken cancellationToken = default) =>
        reservations.ReserveAsync(request, cancellationToken);

    public Task<QuotaSnapshot?> GetSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        snapshotQuery.GetSnapshotAsync(userId, cancellationToken);
}
