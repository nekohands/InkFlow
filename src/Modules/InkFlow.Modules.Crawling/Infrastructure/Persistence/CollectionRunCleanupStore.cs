using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Owns transactional cleanup of collection runs and their children.</summary>
internal sealed class CollectionRunCleanupStore(CrawlingDbContext db)
{
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
        var taskIds = await TasksForRun(id)
            .AsNoTracking()
            .Select(task => task.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (taskIds.Count > 0)
        {
            await db.DeadLetters
                .Where(deadLetter => taskIds.Contains(deadLetter.TaskId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await TasksForRun(id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        db.Runs.Remove(runEntity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> DeleteCancelledAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidateRunIds = await db.Runs
            .AsNoTracking()
            .Where(run => run.Status == (int)CollectionRunStatus.Cancelled)
            .OrderBy(run => run.Id)
            .Select(run => run.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (candidateRunIds.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        // Workers use task -> run lock order; keep cleanup in the same order.
        _ = await db.Tasks
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."tasks"
                WHERE "RunId" = ANY({candidateRunIds})
                FOR UPDATE
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lockedRuns = await db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = ANY({candidateRunIds})
                  AND "Status" = {(int)CollectionRunStatus.Cancelled}
                ORDER BY "Id"
                FOR UPDATE
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var runIds = lockedRuns.Select(run => run.Id).ToArray();
        if (runIds.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var taskIds = await TasksForRuns(runIds)
            .AsNoTracking()
            .Select(task => task.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (taskIds.Length > 0)
        {
            await db.DeadLetters
                .Where(deadLetter => taskIds.Contains(deadLetter.TaskId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            await TasksForRuns(runIds)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var deleted = await db.Runs
            .Where(run => runIds.Contains(run.Id) &&
                          run.Status == (int)CollectionRunStatus.Cancelled)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    private IQueryable<CrawlerTaskEntity> TasksForRun(Guid runId) =>
        db.Tasks.Where(task => task.RunId == runId);

    private IQueryable<CrawlerTaskEntity> TasksForRuns(Guid[] runIds) =>
        db.Tasks.Where(task => task.RunId.HasValue && runIds.Contains(task.RunId.Value));
}
