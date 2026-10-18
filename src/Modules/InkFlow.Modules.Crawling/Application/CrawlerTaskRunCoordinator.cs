using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

internal sealed class CrawlerTaskRunCoordinator(CollectionRunService? collectionRuns)
{
    public async Task<bool> ShouldCancelBeforeExecutionAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(task, cancellationToken).ConfigureAwait(false);
        return IsTerminal(status);
    }

    public async Task<bool> ShouldCancelAfterFailureAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(task, cancellationToken).ConfigureAwait(false);
        return status == CollectionRunStatus.Stopping || IsTerminal(status);
    }

    public static bool IsTerminal(CollectionRunStatus? status) => status is
        CollectionRunStatus.Cancelled or
        CollectionRunStatus.Failed or
        CollectionRunStatus.Stopped or
        CollectionRunStatus.Completed;

    public Task<CollectionRunStatus?> GetStatusAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        return task.Payload.RunId is { } runId && collectionRuns is not null
            ? collectionRuns.GetStatusAsync(runId, cancellationToken)
            : Task.FromResult<CollectionRunStatus?>(null);
    }

    public Task MarkWorkStartedAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        return task.Payload.RunId is { } runId && collectionRuns is not null
            ? collectionRuns.MarkWorkStartedAsync(runId, cancellationToken)
            : Task.CompletedTask;
    }

    public Task ReconcileAsync(
        CrawlerTask task,
        CancellationToken cancellationToken)
    {
        return task.Payload.RunId is { } runId && collectionRuns is not null
            ? collectionRuns.ReconcileAsync(runId, cancellationToken)
            : Task.CompletedTask;
    }
}
