using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Persists row-locked, transactional collection-run aggregate mutations.</summary>
internal sealed class CollectionRunAtomicMutationStore(CrawlingDbContext db)
{
    private readonly CollectionRunTaskProgressReader progressReader = new(db);

    public async Task<CollectionRun?> ApplyControlAsync(
        Guid id,
        string action,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Read and mutate under one row lock so concurrent control retries see
        // the same durable state and cannot overwrite one another.
        var entity = await LoadForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var run = CollectionRunMapper.ToDomain(entity);
        switch (action.Trim().ToLowerInvariant())
        {
            case "pause":
                run.Pause(now);
                break;
            case "resume":
                run.Resume(now);
                break;
            case "stop":
                run.RequestStop(now);
                break;
            case "cancel":
                run.Cancel(now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        CollectionRunMapper.ApplyDomain(run, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return run;
    }

    public async Task<CollectionRun?> ReconcileAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Lock the current run before reading progress so a control command
        // cannot commit between the read and the snapshot save.
        var entity = await LoadForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var progress = await progressReader
            .ReadAsync(id, cancellationToken)
            .ConfigureAwait(false);

        var run = CollectionRunMapper.ToDomain(entity);
        run.Reconcile(progress, now);
        CollectionRunMapper.ApplyDomain(run, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return run;
    }

    public async Task<CollectionRun?> MutateAsync(
        Guid id,
        Action<CollectionRun> mutation,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // All run aggregate writes use the same row lock as controls and
        // reconciliation, so stale snapshots cannot restore an old status.
        var entity = await LoadForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var run = CollectionRunMapper.ToDomain(entity);
        mutation(run);
        CollectionRunMapper.ApplyDomain(run, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return run;
    }

    private async Task<CollectionRunEntity?> LoadForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        // A scoped context may still track a stale entity from an earlier insert.
        // Detach only this run before the lock query so the database row is the
        // snapshot that gets mutated and saved.
        var tracked = db.Runs.Local.FirstOrDefault(run => run.Id == id);
        if (tracked is not null)
        {
            db.Entry(tracked).State = EntityState.Detached;
        }

        return await db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
