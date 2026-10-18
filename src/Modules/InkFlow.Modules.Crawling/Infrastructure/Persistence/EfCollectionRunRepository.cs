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
    private readonly CollectionRunQueryStore queryStore = new(db);
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

    public Task<CollectionRun?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        queryStore.GetAsync(id, cancellationToken);

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

    public Task<CollectionRun?> FindActiveAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default) =>
        queryStore.FindActiveAsync(sourceId, externalBookId, cancellationToken);

    public Task<IReadOnlyList<CollectionRun>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        queryStore.ListAsync(limit, cancellationToken);

    public Task<CollectionRunPage> ListPageAsync(
        int limit,
        CollectionRunCursor? before = null,
        CancellationToken cancellationToken = default) =>
        queryStore.ListPageAsync(limit, before, cancellationToken);

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
