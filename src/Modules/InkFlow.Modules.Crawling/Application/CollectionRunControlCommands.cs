using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

internal sealed class CollectionRunControlCommands(
    ICollectionRunRepository runs,
    TimeProvider clock,
    CollectionRunQueries queries,
    CollectionRunProgressCommands progress)
{
    private const int MaxControlReasonLength = 512;

    public async Task<CollectionRunDeleteOutcome> DeleteFailedAsync(
        Guid runId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length == 0 || normalizedReason.Length > MaxControlReasonLength)
        {
            return CollectionRunDeleteOutcome.Failure(
                "collection-run.reason",
                "a deletion reason between 1 and 512 characters is required.");
        }

        var deleted = await runs.DeleteFailedAsync(runId, cancellationToken).ConfigureAwait(false);
        return deleted switch
        {
            true => CollectionRunDeleteOutcome.Deleted(),
            false => CollectionRunDeleteOutcome.Failure(
                "collection-run.not-failed",
                "only failed collection runs can be deleted."),
            null => CollectionRunDeleteOutcome.Failure(
                "collection-run.not-found",
                "collection run was not found."),
        };
    }

    public async Task<CollectionRunCleanupOutcome> DeleteCancelledAsync(
        string? reason,
        CancellationToken cancellationToken)
    {
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length == 0 || normalizedReason.Length > MaxControlReasonLength)
        {
            return CollectionRunCleanupOutcome.Failure(
                "collection-run.reason",
                "a cleanup reason between 1 and 512 characters is required.");
        }

        return CollectionRunCleanupOutcome.Cleaned(
            await runs.DeleteCancelledAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<CollectionRunControlOutcome> ControlAsync(
        Guid runId,
        string? action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var normalizedAction = action?.Trim().ToLowerInvariant();
        if (normalizedAction is not ("pause" or "resume" or "stop" or "cancel"))
        {
            return CollectionRunControlOutcome.Failure(
                "collection-run.action", "action must be pause, resume, stop, or cancel.");
        }

        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length == 0 || normalizedReason.Length > MaxControlReasonLength)
        {
            return CollectionRunControlOutcome.Failure(
                "collection-run.reason", "a control reason between 1 and 512 characters is required.");
        }

        try
        {
            var run = await runs
                .ApplyControlAsync(
                    runId,
                    normalizedAction,
                    clock.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (run is null)
            {
                return CollectionRunControlOutcome.Failure(
                    "collection-run.not-found", "collection run was not found.");
            }

            if (normalizedAction is "stop" or "cancel" or "resume")
            {
                await progress.ReconcileAsync(run.Id, cancellationToken).ConfigureAwait(false);
            }

            var view = await queries.GetViewAsync(run.Id, cancellationToken).ConfigureAwait(false);
            return new(true, view, null, null);
        }
        catch (InvalidOperationException)
        {
            return CollectionRunControlOutcome.Failure(
                "collection-run.invalid-state",
                "collection run cannot apply this control in its current state.");
        }
    }
}
