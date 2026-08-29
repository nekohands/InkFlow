using InkFlow.BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>
/// PostgreSQL 审计保留实现：在事务内按时间索引分批删除，并以 SKIP LOCKED 避免阻塞追加写入。
/// 数据库触发器只允许带有本事务标记的受控 retention 删除，普通 SQL 删除仍被拒绝。
/// </summary>
public sealed class EfAuditRetentionStore(AuditDbContext db) : IAuditRetentionStore
{
    public async Task<int> DeleteExpiredBatchAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ValidateCutoff(cutoff);
        ValidateBatchSize(batchSize);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // The marker is local to this transaction and is consumed by the audit trigger.
        await db.Database.ExecuteSqlRawAsync(
                """SET LOCAL "inkflow.audit_retention_cleanup" = 'on';""",
                cancellationToken)
            .ConfigureAwait(false);

        var deleted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH candidates AS (
                SELECT "Id"
                  FROM "audit"."events"
                 WHERE "OccurredAt" < {cutoff.ToUniversalTime()}
                 ORDER BY "OccurredAt", "Id"
                 LIMIT {batchSize}
                 FOR UPDATE SKIP LOCKED
            )
            DELETE FROM "audit"."events" AS target
             USING candidates
             WHERE target."Id" = candidates."Id";
            """, cancellationToken).ConfigureAwait(false);

        db.ChangeTracker.Clear();
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    private static void ValidateCutoff(DateTimeOffset cutoff)
    {
        if (cutoff == DateTimeOffset.MinValue || cutoff == DateTimeOffset.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(cutoff));
        }
    }

    private static void ValidateBatchSize(int batchSize)
    {
        if (batchSize is < 1 or > 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
    }
}
