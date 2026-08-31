using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>
/// Transaction-scoped locks serialize scheduler dedupe checks for one stable
/// (source, capability, variable, value) key across API and Scheduler instances.
/// </summary>
internal static class CrawlerTaskAdvisoryLock
{
    private const int NamespaceKey = 1202;

    public static Task AcquireAsync(
        CrawlingDbContext db,
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken)
    {
        var identity = Encoding.UTF8.GetBytes(
            $"{sourceId}\0{(int)capability}\0{variableName}\0{variableValue}");
        var hash = BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(identity));

        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({NamespaceKey}, {hash})",
            cancellationToken);
    }
}

/// <summary>EF Core / Npgsql 仓储实现。租约互斥依赖数据库事务 + 状态条件更新。</summary>
public sealed class EfCrawlerTaskRepository(
    CrawlingDbContext db,
    ITransactionalOutboxWriter outbox) : ICrawlerTaskRepository, ICrawlerTaskRepairRepository
{
    public async Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        db.Tasks.Add(CrawlerTaskMapper.ToEntity(task));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await outbox.EnqueueAsync(
                db,
                CrawlerIntegrationMessages.TaskCreated(task),
                cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CrawlerTask?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Tasks.FindAsync([id], cancellationToken).ConfigureAwait(false);
        return entity is null ? null : CrawlerTaskMapper.ToDomain(entity);
    }

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
        // 使“筛选 + 状态流转”成为一个跨进程原子领取操作。
        var candidates = taskId is { } targetedTaskId
            ? await db.Tasks
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "crawler"."tasks"
                    WHERE "Id" = {targetedTaskId}
                      AND ("RunId" IS NULL OR EXISTS (
                          SELECT 1 FROM "crawler"."runs" r
                          WHERE r."Id" = "RunId"
                            AND r."Status" IN ({(int)CollectionRunStatus.Pending}, {(int)CollectionRunStatus.Running})))
                      AND (("Status" = {(int)CrawlerTaskStatus.Pending}
                            AND ("ScheduledAt" IS NULL OR "ScheduledAt" <= {now}))
                       OR ("Status" IN ({(int)CrawlerTaskStatus.Leased}, {(int)CrawlerTaskStatus.Running})
                           AND "LeaseExpiresAt" IS NOT NULL
                           AND "LeaseExpiresAt" <= {now}))
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : await db.Tasks
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "crawler"."tasks"
                    WHERE ("RunId" IS NULL OR EXISTS (
                              SELECT 1 FROM "crawler"."runs" r
                              WHERE r."Id" = "RunId"
                                AND r."Status" IN ({(int)CollectionRunStatus.Pending}, {(int)CollectionRunStatus.Running})))
                      AND (("Status" = {(int)CrawlerTaskStatus.Pending}
                           AND ("ScheduledAt" IS NULL OR "ScheduledAt" <= {now}))
                       OR ("Status" IN ({(int)CrawlerTaskStatus.Leased}, {(int)CrawlerTaskStatus.Running})
                           AND "LeaseExpiresAt" IS NOT NULL
                           AND "LeaseExpiresAt" <= {now})
                      )
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
            var runEntity = await db.Runs
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "crawler"."runs"
                    WHERE "Id" = {runId}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken)
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
            var runEntity = await db.Runs
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "crawler"."runs"
                    WHERE "Id" = {runId}
                    FOR UPDATE
                    """)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken)
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

    public async Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        var entity = await db.Tasks.FindAsync([task.Id], cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException(
                         $"crawler task {task.Id} does not exist; use {nameof(AddAsync)} first.");

        CrawlerTaskMapper.ApplyDomain(task, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default)
    {
        var entities = await db.Tasks
            .Where(t =>
                (t.RunId == null || db.Runs.Any(r =>
                    r.Id == t.RunId &&
                    (r.Status == (int)CollectionRunStatus.Pending ||
                     r.Status == (int)CollectionRunStatus.Running))) &&
                ((t.Status == (int)CrawlerTaskStatus.Pending &&
                 (t.ScheduledAt == null || t.ScheduledAt <= now)) ||
                ((t.Status == (int)CrawlerTaskStatus.Leased ||
                  t.Status == (int)CrawlerTaskStatus.Running) &&
                 t.LeaseExpiresAt != null && t.LeaseExpiresAt <= now)))
            .OrderBy(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(CrawlerTaskMapper.ToDomain).ToList();
    }

    public async Task AddDeadLetterAsync(DeadLetterTask deadLetter, CancellationToken cancellationToken = default)
    {
        db.DeadLetters.Add(new DeadLetterEntity
        {
            Id = deadLetter.Id,
            TaskId = deadLetter.TaskId,
            SourceId = deadLetter.SourceId,
            Reason = deadLetter.Reason,
            AttemptCount = deadLetter.AttemptCount,
            DeadLetteredAt = deadLetter.DeadLetteredAt,
            ReplayTaskId = deadLetter.ReplayTaskId,
            ReplayedAt = deadLetter.ReplayedAt,
            ReplayRequestedBy = deadLetter.ReplayRequestedBy,
            ReplayReason = deadLetter.ReplayReason,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeadLetterReplayResult> ReplayDeadLetterAsync(
        DeadLetterReplayCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // 重放是修复命令：锁住死信行，避免两个操作员同时创建两个重放任务。
        var deadLetter = await db.DeadLetters
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."dead_letters"
                WHERE "Id" = {command.DeadLetterId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deadLetter is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(DeadLetterReplayStatus.NotFound);
        }

        if (deadLetter.ReplayTaskId is { } existingReplayTaskId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(DeadLetterReplayStatus.AlreadyReplayed, existingReplayTaskId);
        }

        // 锁住原任务并再次核对状态，防止修复入口绕过聚合状态机。
        var taskEntity = await db.Tasks
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."tasks"
                WHERE "Id" = {deadLetter.TaskId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (taskEntity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(DeadLetterReplayStatus.OriginalTaskMissing);
        }

        if (taskEntity.Status != (int)CrawlerTaskStatus.DeadLettered)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(DeadLetterReplayStatus.OriginalTaskNotDeadLettered);
        }

        var originalTask = CrawlerTaskMapper.ToDomain(taskEntity);
        var replayTask = CrawlerTask.Create(
            originalTask.Payload,
            maxAttempts: originalTask.MaxAttempts,
            createdAt: now);

        db.Tasks.Add(CrawlerTaskMapper.ToEntity(replayTask));
        // AddDeadLetterAsync 可能刚在同一个 DbContext 中写入并仍在 Local 缓存；
        // 优先复用已跟踪实例，避免同键实体冲突，同时保留 FOR UPDATE 的锁语义。
        var trackedDeadLetter = db.DeadLetters.Local
            .SingleOrDefault(candidate => candidate.Id == deadLetter.Id);
        if (trackedDeadLetter is not null)
        {
            deadLetter = trackedDeadLetter;
        }
        else
        {
            db.DeadLetters.Attach(deadLetter);
        }

        deadLetter.ReplayTaskId = replayTask.Id;
        deadLetter.ReplayedAt = now;
        deadLetter.ReplayRequestedBy = command.RequestedBy;
        deadLetter.ReplayReason = command.ReplayReason;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(DeadLetterReplayStatus.Replayed, replayTask.Id);
    }

    public async Task<bool> HasActiveTaskAsync(
        string sourceId, SourceCapability capability, CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            (int)CrawlerTaskStatus.Pending,
            (int)CrawlerTaskStatus.Leased,
            (int)CrawlerTaskStatus.Running,
        };

        return await db.Tasks
            .AnyAsync(t =>
                t.SourceId == sourceId &&
                t.Capability == (int)capability &&
                activeStatuses.Contains(t.Status),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasConflictingTaskAsync(
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default) =>
        await HasBlockingTaskForCollectionRunAsync(
                sourceId,
                capability,
                variableName,
                variableValue,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> TryAddIfNoConflictingTaskAsync(
        CrawlerTask task,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false)
    {
        ValidateDedupeTask(task, variableName, variableValue);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var inserted = await TryAddIfNoConflictingTaskInTransactionAsync(
                task,
                variableName,
                variableValue,
                cancellationToken,
                ignoreDeadLettered)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }

    public async Task<bool> TryAddIfNoConflictingTaskForCollectionRunAsync(
        CrawlerTask task,
        Guid runId,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false)
    {
        ValidateDedupeTask(task, variableName, variableValue);
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("collection run ID must not be empty.", nameof(runId));
        }

        if (task.Payload.RunId != runId)
        {
            throw new ArgumentException(
                "the task must reference the requested collection run.",
                nameof(runId));
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Controls and this enqueue gate lock the same parent row. If a stop or
        // cancel commits first, the gate re-reads the durable state and rejects
        // the follow-up; if enqueue commits first, the later control observes it.
        var runEntity = await db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = {runId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (runEntity is null || runEntity.Status is not (
                (int)CollectionRunStatus.Pending or
                (int)CollectionRunStatus.Running or
                (int)CollectionRunStatus.Paused))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var inserted = await TryAddIfNoConflictingTaskInTransactionAsync(
                task,
                variableName,
                variableValue,
                cancellationToken,
                ignoreDeadLettered)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }

    private async Task<bool> TryAddIfNoConflictingTaskInTransactionAsync(
        CrawlerTask task,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken,
        bool ignoreDeadLettered)
    {
        await CrawlerTaskAdvisoryLock
            .AcquireAsync(
                db,
                task.Payload.SourceId,
                task.Payload.Capability,
                variableName,
                variableValue,
                cancellationToken)
            .ConfigureAwait(false);

        if (await HasBlockingTaskForCollectionRunAsync(
                task.Payload.SourceId,
                task.Payload.Capability,
                variableName,
                variableValue,
                cancellationToken,
                ignoreDeadLettered)
                .ConfigureAwait(false))
        {
            return false;
        }

        db.Tasks.Add(CrawlerTaskMapper.ToEntity(task));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await outbox.EnqueueAsync(
                db,
                CrawlerIntegrationMessages.TaskCreated(task),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static void ValidateDedupeTask(
        CrawlerTask task,
        string variableName,
        string variableValue)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("variable name must not be empty.", nameof(variableName));
        }

        if (!task.Payload.Variables.TryGetValue(variableName, out var taskVariableValue) ||
            taskVariableValue != variableValue)
        {
            throw new ArgumentException(
                "the task must contain the requested dedupe variable and value.",
                nameof(variableValue));
        }
    }

    public async Task<bool> HasBlockingTaskForCollectionRunAsync(
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false)
    {
        var blockingStatuses = new[]
        {
            (int)CrawlerTaskStatus.Pending,
            (int)CrawlerTaskStatus.Leased,
            (int)CrawlerTaskStatus.Running,
            (int)CrawlerTaskStatus.DeadLettered,
        };

        // Variables 为 jsonb 字典,EF 无法翻译取值比较;先按 (source, capability, 状态)
        // 服务端裁剪,再在内存中匹配变量。单来源单能力的任务量级有限。
        var candidates = await db.Tasks
            .Where(t => t.SourceId == sourceId &&
                        t.Capability == (int)capability &&
                        blockingStatuses.Contains(t.Status) &&
                        (ignoreDeadLettered ||
                         t.Status != (int)CrawlerTaskStatus.DeadLettered ||
                         !db.DeadLetters.Any(d => d.TaskId == t.Id && d.ReplayTaskId != null)))
            .Select(t => t.Variables)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates.Any(variables =>
            variables.TryGetValue(variableName, out var value) && value == variableValue);
    }

    public async Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(int limit, CancellationToken cancellationToken = default)
    {
        var entities = await db.DeadLetters
            .OrderByDescending(d => d.DeadLetteredAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(e => new DeadLetterTask(
                e.Id,
                e.TaskId,
                e.SourceId,
                e.Reason,
                e.AttemptCount,
                e.DeadLetteredAt,
                e.ReplayTaskId,
                e.ReplayedAt,
                e.ReplayRequestedBy,
                e.ReplayReason))
            .ToList();
    }
}
