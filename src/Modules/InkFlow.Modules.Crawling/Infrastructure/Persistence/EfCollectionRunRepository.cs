using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>采集运行仓储；子任务明细仍从 crawler.tasks 实时聚合。</summary>
public sealed class EfCollectionRunRepository(
    CrawlingDbContext db,
    ITransactionalOutboxWriter outbox) : ICollectionRunRepository
{
    private readonly CollectionRunCleanupStore cleanupStore = new(db);
    private readonly CollectionRunBootstrapStore bootstrapStore = new(db, outbox);
    private readonly CollectionRunTaskProgressReader progressReader = new(db);
    private readonly CollectionRunAtomicMutationStore mutationStore = new(db);

    public async Task AddAsync(CollectionRun run, CancellationToken cancellationToken = default)
    {
        db.Runs.Add(CollectionRunMapper.ToEntity(run));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryAddAsync(
        CollectionRun run,
        CancellationToken cancellationToken = default)
    {
        var entity = CollectionRunMapper.ToEntity(run);
        db.Runs.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            })
        {
            // The unique partial index is the authoritative concurrency gate.
            // Detach the failed insert so this scoped context remains usable for
            // the subsequent active-run lookup in the service.
            db.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public Task<bool> TryAddWithInitialTaskAsync(
        CollectionRun run,
        CrawlerTask task,
        CancellationToken cancellationToken = default) =>
        bootstrapStore.TryAddAsync(run, task, cancellationToken);

    public async Task<CollectionRun?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Runs
            .AsNoTracking()
            .SingleOrDefaultAsync(run => run.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : CollectionRunMapper.ToDomain(entity);
    }

    public Task<CollectionRun?> ApplyControlAsync(
        Guid id,
        string action,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
        => mutationStore.ApplyControlAsync(id, action, now, cancellationToken);

    public Task<CollectionRun?> ReconcileAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
        => mutationStore.ReconcileAsync(id, now, cancellationToken);

    public Task<CollectionRun?> MutateAsync(
        Guid id,
        Action<CollectionRun> mutation,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
        => mutationStore.MutateAsync(id, mutation, now, cancellationToken);

    public async Task<CollectionRun?> FindActiveAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            (int)CollectionRunStatus.Pending,
            (int)CollectionRunStatus.Running,
            (int)CollectionRunStatus.Paused,
            (int)CollectionRunStatus.Stopping,
        };

        var entity = await db.Runs
            .AsNoTracking()
            .Where(run => run.SourceId == sourceId &&
                          run.ExternalBookId == externalBookId &&
                          activeStatuses.Contains(run.Status))
            .OrderByDescending(run => run.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : CollectionRunMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<CollectionRun>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var page = await ListPageAsync(limit, null, cancellationToken).ConfigureAwait(false);
        return page.Entries;
    }

    public async Task<CollectionRunPage> ListPageAsync(
        int limit,
        CollectionRunCursor? before = null,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var query = db.Runs
            .AsNoTracking()
            .AsQueryable();
        if (before is not null)
        {
            query = query.Where(run =>
                run.UpdatedAt < before.UpdatedAt ||
                (run.UpdatedAt == before.UpdatedAt &&
                 run.Id.CompareTo(before.Id) < 0));
        }

        var entities = await query
            .OrderByDescending(run => run.UpdatedAt)
            .ThenByDescending(run => run.Id)
            .Take(safeLimit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasMore = entities.Count > safeLimit;
        var pageEntities = entities.Take(safeLimit).ToList();
        var entries = pageEntities.Select(CollectionRunMapper.ToDomain).ToList();
        var nextCursor = hasMore && pageEntities.Count > 0
            ? new CollectionRunCursor(
                pageEntities[^1].UpdatedAt,
                pageEntities[^1].Id)
            : null;

        return new(entries, nextCursor);
    }

    public Task<bool?> DeleteFailedAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        cleanupStore.DeleteFailedAsync(id, cancellationToken);

    public Task<int> DeleteCancelledAsync(
        CancellationToken cancellationToken = default) =>
        cleanupStore.DeleteCancelledAsync(cancellationToken);

    public async Task SaveAsync(CollectionRun run, CancellationToken cancellationToken = default)
    {
        var entity = await db.Runs
            .SingleOrDefaultAsync(candidate => candidate.Id == run.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"collection run {run.Id} does not exist; use {nameof(AddAsync)} first.");

        CollectionRunMapper.ApplyDomain(run, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<CollectionRunTaskProgress> GetTaskProgressAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        progressReader.ReadAsync(runId, cancellationToken);
}
