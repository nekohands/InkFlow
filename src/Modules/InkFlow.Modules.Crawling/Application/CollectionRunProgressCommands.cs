using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

internal sealed class CollectionRunProgressCommands(
    ICollectionRunRepository runs,
    TimeProvider clock)
{
    public Task SetCanonicalBookAsync(
        Guid runId,
        Guid canonicalBookId,
        CancellationToken cancellationToken) =>
        MutateRunAsync(
            runId,
            (run, now) => run.SetCanonicalBook(canonicalBookId, now),
            cancellationToken);

    public Task AdvanceStageAsync(
        Guid runId,
        CollectionRunStage stage,
        CancellationToken cancellationToken) =>
        MutateRunAsync(
            runId,
            (run, now) => run.AdvanceTo(stage, now),
            cancellationToken);

    public Task MarkWorkStartedAsync(
        Guid runId,
        CancellationToken cancellationToken) =>
        MutateRunAsync(
            runId,
            (run, now) => run.MarkWorkStarted(now),
            cancellationToken);

    public Task ReconcileAsync(
        Guid runId,
        CancellationToken cancellationToken) =>
        runs.ReconcileAsync(runId, clock.GetUtcNow(), cancellationToken);

    private async Task MutateRunAsync(
        Guid runId,
        Action<CollectionRun, DateTimeOffset> mutation,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (await runs
                .MutateAsync(
                    runId,
                    run => mutation(run, now),
                    now,
                    cancellationToken)
                .ConfigureAwait(false) is null)
        {
            throw new InvalidOperationException($"collection run {runId} was not found.");
        }
    }
}
