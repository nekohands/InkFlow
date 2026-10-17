using InkFlow.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Billing.Application;

/// <summary>读取并按当前计划版本校验月度额度快照，必要时从持久化用量重建。</summary>
internal sealed class QuotaSnapshotQuery(
    BillingDbContext db,
    IEntitlementService entitlements,
    IQuotaSnapshotCache cache,
    TimeProvider clock)
{
    public async Task<QuotaSnapshot?> GetSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entitlement = await entitlements.GetForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        if (entitlement is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var periodStart = QuotaPeriod.StartOfMonth(now);
        var cached = await cache.GetAsync(userId, periodStart, cancellationToken).ConfigureAwait(false);
        if (cached is not null
            && cached.UserId == userId
            && cached.PeriodStart == periodStart
            && cached.PeriodEnd > now
            && string.Equals(cached.PlanCode, entitlement.Plan.Code, StringComparison.Ordinal)
            && cached.PlanVersion == entitlement.Plan.Version
            && cached.LimitUnits == entitlement.Plan.MonthlyQuotaUnits
            && string.Equals(
                cached.AlgorithmVersion,
                entitlement.Plan.QuotaAlgorithmVersion,
                StringComparison.Ordinal))
        {
            return cached;
        }

        var used = await db.UsagePeriods
            .Where(x => x.UserId == userId && x.PeriodStart == periodStart)
            .Select(x => (long?)x.UsedUnits)
            .SumAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0;
        var snapshot = new QuotaSnapshot(
            userId,
            entitlement.Plan.Code,
            entitlement.Plan.Version,
            periodStart,
            periodStart.AddMonths(1),
            entitlement.Plan.MonthlyQuotaUnits,
            used,
            Math.Max(0, entitlement.Plan.MonthlyQuotaUnits - used),
            entitlement.Plan.QuotaAlgorithmVersion);
        await cache.SetAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }
}
