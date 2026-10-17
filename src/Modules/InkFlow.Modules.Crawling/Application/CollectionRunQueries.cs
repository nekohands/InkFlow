using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

internal sealed class CollectionRunQueries(ICollectionRunRepository runs)
{
    public async Task<CollectionRunView?> GetViewAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return run is null ? null : await BuildViewAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CollectionRunView>> ListViewsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var page = await ListPageViewsAsync(limit, null, cancellationToken).ConfigureAwait(false);
        return page.Entries;
    }

    public async Task<CollectionRunViewPage> ListPageViewsAsync(
        int limit,
        CollectionRunCursor? before,
        CancellationToken cancellationToken)
    {
        var page = await runs
            .ListPageAsync(limit, before, cancellationToken)
            .ConfigureAwait(false);
        var views = new List<CollectionRunView>(page.Entries.Count);
        foreach (var run in page.Entries)
        {
            views.Add(await BuildViewAsync(run, cancellationToken).ConfigureAwait(false));
        }

        return new(views, page.NextCursor);
    }

    public async Task<bool> CanScheduleFollowUpAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return run?.CanScheduleFollowUp == true;
    }

    public async Task<CollectionRunStatus?> GetStatusAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return run?.Status;
    }

    internal async Task<CollectionRunView> BuildViewAsync(
        CollectionRun run,
        CancellationToken cancellationToken)
    {
        var progress = await runs
            .GetTaskProgressAsync(run.Id, cancellationToken)
            .ConfigureAwait(false);
        return new(
            run.Id,
            run.SourceId,
            run.ExternalBookId,
            run.InputUrl,
            run.CanonicalBookId,
            run.Status,
            run.Stage,
            progress.TotalTaskCount,
            progress.CompletedTaskCount,
            progress.DeadLetteredTaskCount,
            progress.PendingTaskCount,
            progress.InFlightTaskCount,
            progress.CancelledTaskCount,
            progress.RemainingTaskCount,
            run.LastError,
            run.CreatedAt,
            run.UpdatedAt);
    }
}
