using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>EF Core / Npgsql 仓储实现。租约互斥依赖数据库事务 + 状态条件更新。</summary>
public sealed class EfCrawlerTaskRepository(
    CrawlingDbContext db,
    ITransactionalOutboxWriter outbox) : ICrawlerTaskRepository, ICrawlerTaskRepairRepository
{
    private readonly CrawlerTaskDeadLetterStore deadLetterStore = new(db);
    private readonly CrawlerTaskReplayStore replayStore = new(db);
    private readonly CrawlerTaskDedupeStore dedupeStore = new(db, outbox);
    private readonly CrawlerTaskLeaseStore leaseStore = new(db);

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

    public Task<CrawlerTask?> TryLeaseAsync(
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) =>
        leaseStore.TryLeaseAsync(now, owner, leaseDuration, cancellationToken);

    public Task<CrawlerTask?> TryLeaseAsync(
        Guid taskId,
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) =>
        leaseStore.TryLeaseAsync(taskId, now, owner, leaseDuration, cancellationToken);

    public Task<bool> TryMarkRunningAsync(
        CrawlerTask task,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        leaseStore.TryMarkRunningAsync(task, now, cancellationToken);

    public Task<bool> TryRenewLeaseAsync(
        CrawlerTask task,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) =>
        leaseStore.TryRenewLeaseAsync(task, now, leaseDuration, cancellationToken);

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

    public Task AddDeadLetterAsync(
        DeadLetterTask deadLetter,
        CancellationToken cancellationToken = default) =>
        deadLetterStore.AddAsync(deadLetter, cancellationToken);

    public Task AddDeadLetterWithTaskAsync(
        DeadLetterTask deadLetter,
        CrawlerTask task,
        CancellationToken cancellationToken = default) =>
        deadLetterStore.AddWithTaskAsync(deadLetter, task, cancellationToken);

    public Task<DeadLetterReplayResult> ReplayDeadLetterAsync(
        DeadLetterReplayCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        replayStore.ReplayAsync(command, now, cancellationToken);

    public Task<bool> HasActiveTaskAsync(
        string sourceId, SourceCapability capability, CancellationToken cancellationToken = default) =>
        dedupeStore.HasActiveAsync(sourceId, capability, cancellationToken);

    public Task<bool> HasConflictingTaskAsync(
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default) =>
        dedupeStore.HasConflictingAsync(
            sourceId, capability, variableName, variableValue, cancellationToken);

    public Task<bool> TryAddIfNoConflictingTaskAsync(
        CrawlerTask task,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false) =>
        dedupeStore.TryAddIfNoConflictingAsync(
            task, variableName, variableValue, cancellationToken, ignoreDeadLettered);

    public Task<bool> TryAddIfNoConflictingTaskForCollectionRunAsync(
        CrawlerTask task,
        Guid runId,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false) =>
        dedupeStore.TryAddIfNoConflictingForCollectionRunAsync(
            task, runId, variableName, variableValue, cancellationToken, ignoreDeadLettered);

    public Task<bool> HasBlockingTaskForCollectionRunAsync(
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false) =>
        dedupeStore.HasBlockingAsync(
            sourceId,
            capability,
            variableName,
            variableValue,
            cancellationToken,
            ignoreDeadLettered);

    public Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        deadLetterStore.ListAsync(limit, cancellationToken);
}
