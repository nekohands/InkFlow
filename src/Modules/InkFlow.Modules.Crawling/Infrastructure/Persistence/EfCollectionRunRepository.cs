using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>采集运行仓储；子任务明细仍从 crawler.tasks 实时聚合。</summary>
public sealed class EfCollectionRunRepository(CrawlingDbContext db) : ICollectionRunRepository
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
        var safeLimit = Math.Clamp(limit, 1, 200);
        var entities = await db.Runs
            .AsNoTracking()
            .OrderByDescending(run => run.UpdatedAt)
            .ThenByDescending(run => run.Id)
            .Take(safeLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(CollectionRunMapper.ToDomain).ToList();
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
