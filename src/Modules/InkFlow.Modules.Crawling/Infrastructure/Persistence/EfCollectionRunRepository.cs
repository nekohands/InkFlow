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

    public async Task<bool> TryAddWithInitialTaskAsync(
        CollectionRun run,
        CrawlerTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(task);
        if (task.Payload.RunId != run.Id)
        {
            throw new ArgumentException(
                "the initial crawler task must reference the collection run.",
                nameof(task));
        }

        if (!string.Equals(task.Payload.SourceId, run.SourceId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "the initial crawler task must use the collection run source.",
                nameof(task));
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var runEntity = CollectionRunMapper.ToEntity(run);
        var taskEntity = CrawlerTaskMapper.ToEntity(task);
        db.Runs.Add(runEntity);
        db.Tasks.Add(taskEntity);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await outbox.EnqueueAsync(
                    db,
                    CrawlerIntegrationMessages.TaskCreated(task),
                    cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception)
            when (IsActiveRunConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.Entry(runEntity).State = EntityState.Detached;
            db.Entry(taskEntity).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsActiveRunConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "UX_runs_active_source_book",
        };

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
        var entity = await db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
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
        var entity = await db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var progressStatuses = await db.Tasks
            .AsNoTracking()
            .Where(task => task.RunId == id)
            .Select(task => task.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var progress = new CollectionRunTaskProgress(
            progressStatuses.Count,
            progressStatuses.Count(status => status == (int)CrawlerTaskStatus.Pending),
            progressStatuses.Count(status => status == (int)CrawlerTaskStatus.Leased),
            progressStatuses.Count(status => status == (int)CrawlerTaskStatus.Running),
            progressStatuses.Count(status => status == (int)CrawlerTaskStatus.Completed),
            progressStatuses.Count(status => status == (int)CrawlerTaskStatus.DeadLettered),
            progressStatuses.Count(status => status == (int)CrawlerTaskStatus.Cancelled));

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
        var entity = await db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
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

    public async Task<bool?> DeleteFailedAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Workers lock task then run; use the same order to avoid a delete/lease deadlock.
        _ = await db.Tasks
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."tasks"
                WHERE "RunId" = {id}
                FOR UPDATE
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var runEntity = await db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (runEntity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (runEntity.Status != (int)CollectionRunStatus.Failed)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        // A follow-up writer may have inserted a task before the run lock was acquired.
        var taskIds = await db.Tasks
            .AsNoTracking()
            .Where(task => task.RunId == id)
            .Select(task => task.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (taskIds.Count > 0)
        {
            await db.DeadLetters
                .Where(deadLetter => taskIds.Contains(deadLetter.TaskId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await db.Tasks
                .Where(task => task.RunId == id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        db.Runs.Remove(runEntity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

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

    public async Task<CollectionRunTaskProgress> GetTaskProgressAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var statuses = await db.Tasks
            .AsNoTracking()
            .Where(task => task.RunId == runId)
            .Select(task => task.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new(
            statuses.Count,
            statuses.Count(status => status == (int)CrawlerTaskStatus.Pending),
            statuses.Count(status => status == (int)CrawlerTaskStatus.Leased),
            statuses.Count(status => status == (int)CrawlerTaskStatus.Running),
            statuses.Count(status => status == (int)CrawlerTaskStatus.Completed),
            statuses.Count(status => status == (int)CrawlerTaskStatus.DeadLettered),
            statuses.Count(status => status == (int)CrawlerTaskStatus.Cancelled));
    }
}
