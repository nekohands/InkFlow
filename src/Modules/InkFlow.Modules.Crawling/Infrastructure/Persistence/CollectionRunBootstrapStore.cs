using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Persists a collection run, its initial task and TaskCreated atomically.</summary>
internal sealed class CollectionRunBootstrapStore(
    CrawlingDbContext db,
    ITransactionalOutboxWriter outbox)
{
    public async Task<bool> TryAddAsync(
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
}
