using System.Data;
using System.Data.Common;
using InkFlow.Modules.Billing.Domain;
using InkFlow.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Billing.Application;

public sealed class QuotaService(
    BillingDbContext db,
    IEntitlementService entitlements,
    IQuotaSnapshotCache cache,
    TimeProvider clock) : IQuotaService
{
    public async Task<QuotaReservationResult> ReserveAsync(
        QuotaReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId == Guid.Empty || request.ApplicationId == Guid.Empty || request.ApiKeyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Operation) || request.Units is < 1 or > 1_000 ||
            string.IsNullOrWhiteSpace(request.TraceId))
        {
            return new QuotaReservationResult(QuotaReservationStatus.InvalidRequest);
        }

        var entitlement = await entitlements.GetForUserAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (entitlement is null || !entitlement.Plan.Entitlements.Contains(
                CommercialEntitlements.DeveloperCatalogRead,
                StringComparer.Ordinal))
        {
            return new QuotaReservationResult(QuotaReservationStatus.Forbidden);
        }

        var now = clock.GetUtcNow();
        var periodStart = StartOfMonth(now);
        var periodEnd = periodStart.AddMonths(1);

        try
        {
            await using var transaction = await db.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "billing"."usage_periods"
                    ("UserId", "PeriodStart", "UsedUnits", "UpdatedAt")
                VALUES
                    ({request.UserId}, {periodStart}, 0, {now})
                ON CONFLICT ("UserId", "PeriodStart") DO NOTHING
                """, cancellationToken).ConfigureAwait(false);

            var period = await db.UsagePeriods
                .FromSqlInterpolated($"""
                    SELECT * FROM "billing"."usage_periods"
                    WHERE "UserId" = {request.UserId}
                      AND "PeriodStart" = {periodStart}
                    FOR UPDATE
                    """)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);

            if (period.UsedUnits > entitlement.Plan.MonthlyQuotaUnits - request.Units)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new QuotaReservationResult(
                    QuotaReservationStatus.Exceeded,
                    new QuotaSnapshot(
                        request.UserId,
                        entitlement.Plan.Code,
                        entitlement.Plan.Version,
                        periodStart,
                        periodEnd,
                        entitlement.Plan.MonthlyQuotaUnits,
                        period.UsedUnits,
                        Math.Max(0, entitlement.Plan.MonthlyQuotaUnits - period.UsedUnits),
                        entitlement.Plan.QuotaAlgorithmVersion));
            }

            period.UsedUnits += request.Units;
            period.UpdatedAt = now;
            db.UsageLedger.Add(BillingMapper.ToEntity(UsageLedgerEntry.Create(
                request.UserId,
                request.ApplicationId,
                request.ApiKeyId,
                periodStart,
                request.Operation,
                request.Units,
                entitlement.Plan.QuotaAlgorithmVersion,
                now,
                request.TraceId)));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var snapshot = new QuotaSnapshot(
                request.UserId,
                entitlement.Plan.Code,
                entitlement.Plan.Version,
                periodStart,
                periodEnd,
                entitlement.Plan.MonthlyQuotaUnits,
                period.UsedUnits,
                Math.Max(0, entitlement.Plan.MonthlyQuotaUnits - period.UsedUnits),
                entitlement.Plan.QuotaAlgorithmVersion);
            // PostgreSQL 已提交后，缓存只是尽力更新；取消请求不能把成功的计费事实伪装成失败。
            await cache.SetAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
            return new QuotaReservationResult(QuotaReservationStatus.Reserved, snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return new QuotaReservationResult(QuotaReservationStatus.Unavailable);
        }
        catch (DbException)
        {
            return new QuotaReservationResult(QuotaReservationStatus.Unavailable);
        }
    }

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
        var periodStart = StartOfMonth(now);
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

    private static DateTimeOffset StartOfMonth(DateTimeOffset value) =>
        new(value.Year, value.Month, 1, 0, 0, 0, TimeSpan.Zero);
}
