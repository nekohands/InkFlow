using System.Data;
using System.Data.Common;
using InkFlow.Modules.Billing.Domain;
using InkFlow.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Billing.Application;

/// <summary>以事务锁定月度用量并追加审计账本的额度预留用例。</summary>
internal sealed class QuotaReservationUseCase(
    BillingDbContext db,
    IEntitlementService entitlements,
    IQuotaSnapshotCache cache,
    TimeProvider clock)
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
        var periodStart = QuotaPeriod.StartOfMonth(now);
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
}
