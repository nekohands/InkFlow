using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>EF Core / Npgsql 仓储实现。租约互斥依赖数据库事务 + 状态条件更新。</summary>
public sealed class EfCrawlerTaskRepository(CrawlingDbContext db) : ICrawlerTaskRepository
{
    public async Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        db.Tasks.Add(CrawlerTaskMapper.ToEntity(task));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // PostgreSQL 在事务内锁定候选行并跳过已被其他 Worker 锁住的行，
        // 使“筛选 + 状态流转”成为一个跨进程原子领取操作。
        var candidates = await db.Tasks
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."tasks"
                WHERE ("Status" = {(int)CrawlerTaskStatus.Pending}
                       AND ("ScheduledAt" IS NULL OR "ScheduledAt" <= {now}))
                   OR ("Status" IN ({(int)CrawlerTaskStatus.Leased}, {(int)CrawlerTaskStatus.Running})
                       AND "LeaseExpiresAt" IS NOT NULL
                       AND "LeaseExpiresAt" <= {now})
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
                (t.Status == (int)CrawlerTaskStatus.Pending &&
                 (t.ScheduledAt == null || t.ScheduledAt <= now)) ||
                ((t.Status == (int)CrawlerTaskStatus.Leased ||
                  t.Status == (int)CrawlerTaskStatus.Running) &&
                 t.LeaseExpiresAt != null && t.LeaseExpiresAt <= now))
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
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken = default)
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
                        blockingStatuses.Contains(t.Status))
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
            .Select(e => new DeadLetterTask(e.Id, e.TaskId, e.SourceId, e.Reason, e.AttemptCount, e.DeadLetteredAt))
            .ToList();
    }
}
