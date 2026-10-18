using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>
/// Owns the durable crawler lease lifecycle: claiming a task, validating its
/// collection run before starting, and renewing the active lease.
/// </summary>
internal sealed class CrawlerTaskLeaseStore(CrawlingDbContext db)
{
    public async Task<CrawlerTask?> TryLeaseAsync(
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) =>
        await TryLeaseCoreAsync(
                taskId: null,
                now,
                owner,
                leaseDuration,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<CrawlerTask?> TryLeaseAsync(
        Guid taskId,
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) =>
        await TryLeaseCoreAsync(
                taskId: taskId,
                now,
                owner,
                leaseDuration,
                cancellationToken)
            .ConfigureAwait(false);

    private async Task<CrawlerTask?> TryLeaseCoreAsync(
        Guid? taskId,
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("crawler task ID must not be empty.", nameof(taskId));
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // PostgreSQL 在事务内锁定候选行并跳过已被其他 Worker 锁住的行，
        // 使“筛选 + 状态流转”成为一个跨进程原子领取操作。目标任务使用布尔参数
        // 参与同一候选策略，避免定向领取与普通领取复制两套可领取条件。
        var hasTarget = taskId.HasValue;
        var targetId = taskId.GetValueOrDefault();
        var candidates = await db.Tasks
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."tasks"
                WHERE ({hasTarget} = FALSE OR "Id" = {targetId})
                  AND ("RunId" IS NULL OR EXISTS (
                      SELECT 1 FROM "crawler"."runs" r
                      WHERE r."Id" = "RunId"
                        AND r."Status" IN ({(int)CollectionRunStatus.Pending}, {(int)CollectionRunStatus.Running})))
                  AND (("Status" = {(int)CrawlerTaskStatus.Pending}
                        AND ("ScheduledAt" IS NULL OR "ScheduledAt" <= {now}))
                   OR ("Status" IN ({(int)CrawlerTaskStatus.Leased}, {(int)CrawlerTaskStatus.Running})
                       AND "LeaseExpiresAt" IS NOT NULL
                       AND "LeaseExpiresAt" <= {now}))
                ORDER BY "CreatedAt", "Id"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = candidates.SingleOrDefault();
        if (entity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (entity.RunId is { } runId)
        {
            // The candidate query can observe a parent run before a concurrent
            // control transaction commits. Lock and re-read that parent before
            // mutating the task so a lease cannot commit after pause/stop/cancel.
            var runEntity = await LockParentRunAsync(runId, cancellationToken)
                .ConfigureAwait(false);
            if (runEntity is null || runEntity.Status is not (
                    (int)CollectionRunStatus.Pending or
                    (int)CollectionRunStatus.Running))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }
        }

        var task = CrawlerTaskMapper.ToDomain(entity);
        if (task.Status is CrawlerTaskStatus.Leased or CrawlerTaskStatus.Running)
        {
            task.ReleaseExpiredLease(now);
        }

        task.Lease(owner, now, leaseDuration);
        CrawlerTaskMapper.ApplyDomain(task, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task<bool> TryMarkRunningAsync(
        CrawlerTask task,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Lock the task before the parent run. Lease recovery uses the same
        // order; control commands only lock the parent, so a control that wins
        // the race is observed after its transaction commits.
        var taskEntity = await db.Tasks
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."tasks"
                WHERE "Id" = {task.Id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (taskEntity is null ||
            taskEntity.Status != (int)CrawlerTaskStatus.Leased ||
            task.Payload.RunId != taskEntity.RunId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (taskEntity.RunId is { } runId)
        {
            // No-tracking is intentional: a scoped DbContext may still hold an
            // older run entity from a previous application operation. The lock
            // query must classify the durable row returned by PostgreSQL.
            var runEntity = await LockParentRunAsync(
                    runId,
                    cancellationToken,
                    asNoTracking: true)
                .ConfigureAwait(false);

            if (runEntity is null || runEntity.Status is not (
                    (int)CollectionRunStatus.Pending or
                    (int)CollectionRunStatus.Running or
                    (int)CollectionRunStatus.Paused or
                    (int)CollectionRunStatus.Stopping))
            {
                var rejectedTask = CrawlerTaskMapper.ToDomain(taskEntity);
                rejectedTask.Cancel(now);
                CrawlerTaskMapper.ApplyDomain(rejectedTask, taskEntity);
                if (task.Status is not (CrawlerTaskStatus.Completed or
                    CrawlerTaskStatus.DeadLettered or
                    CrawlerTaskStatus.Cancelled))
                {
                    task.Cancel(now);
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (runEntity.Status == (int)CollectionRunStatus.Pending)
            {
                // Usually CollectionRunService already performs this mutation;
                // keeping it here makes the task start safe when that optional
                // service is not present and keeps the parent/task transition
                // within the same transaction as the Running write.
                await db.Database
                    .ExecuteSqlInterpolatedAsync($"""
                        UPDATE "crawler"."runs"
                        SET "Status" = {(int)CollectionRunStatus.Running},
                            "UpdatedAt" = {now}
                        WHERE "Id" = {runId}
                        """, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        task.MarkRunning(now);
        CrawlerTaskMapper.ApplyDomain(task, taskEntity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> TryRenewLeaseAsync(
        CrawlerTask task,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "lease duration must be positive.");
        }

        if (task.Status is not (CrawlerTaskStatus.Leased or CrawlerTaskStatus.Running) ||
            task.LeaseOwner is not { Length: > 0 } owner ||
            task.LeaseExpiresAt is not { } expiry ||
            expiry <= now)
        {
            return false;
        }

        now = now.ToUniversalTime();
        var leaseExpiresAt = now + leaseDuration;
        var affected = await db.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE "crawler"."tasks"
                SET "LeaseExpiresAt" = {leaseExpiresAt},
                    "UpdatedAt" = {now}
                WHERE "Id" = {task.Id}
                  AND "LeaseOwner" = {owner}
                  AND "Status" IN ({(int)CrawlerTaskStatus.Leased}, {(int)CrawlerTaskStatus.Running})
                  AND "LeaseExpiresAt" > {now}
                """, cancellationToken)
            .ConfigureAwait(false);

        if (affected != 1)
        {
            return false;
        }

        task.RenewLease(now, leaseDuration);
        return true;
    }

    private async Task<CollectionRunEntity?> LockParentRunAsync(
        Guid runId,
        CancellationToken cancellationToken,
        bool asNoTracking = false)
    {
        var query = db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = {runId}
                FOR UPDATE
                """);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
