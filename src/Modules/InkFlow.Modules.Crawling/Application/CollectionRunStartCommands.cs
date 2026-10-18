using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

internal sealed class CollectionRunStartCommands(
    SourceBookUrlResolver urlResolver,
    ICollectionRunRepository runs,
    TimeProvider clock,
    CollectionRunQueries queries,
    CollectionRunProgressCommands progress)
{
    public async Task<CollectionRunStartOutcome> StartFromUrlAsync(
        string? inputUrl,
        CancellationToken cancellationToken)
    {
        var resolved = await urlResolver
            .ResolveAsync(inputUrl, cancellationToken)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess)
        {
            return CollectionRunStartOutcome.Failure(
                resolved.ErrorCode ?? "source-url.unresolved",
                resolved.Error ?? "book URL could not be resolved.");
        }

        var existing = await runs
            .FindActiveAsync(
                resolved.SourceId!,
                resolved.ExternalBookId!,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return new(
                true,
                await queries.BuildViewAsync(existing, cancellationToken).ConfigureAwait(false),
                true,
                null,
                null);
        }

        var now = clock.GetUtcNow();
        var run = CollectionRun.Create(
            resolved.SourceId!,
            resolved.ExternalBookId!,
            resolved.NormalizedUrl!,
            now);
        var initialTask = CrawlerTask.Create(
            new CrawlPayload(
                run.SourceId,
                SourceCapability.BookInfo,
                new Dictionary<string, string>
                {
                    ["bookId"] = run.ExternalBookId,
                    ["reason"] = "direct-url",
                },
                RunId: run.Id),
            createdAt: now);
        if (!await runs
                .TryAddWithInitialTaskAsync(run, initialTask, cancellationToken)
                .ConfigureAwait(false))
        {
            var concurrent = await runs
                .FindActiveAsync(
                    resolved.SourceId!,
                    resolved.ExternalBookId!,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "an active collection run disappeared during concurrent creation.");
            return new(
                true,
                await queries.BuildViewAsync(concurrent, cancellationToken).ConfigureAwait(false),
                true,
                null,
                null);
        }

        await progress.ReconcileAsync(run.Id, cancellationToken).ConfigureAwait(false);
        var created = await queries.GetViewAsync(run.Id, cancellationToken).ConfigureAwait(false);
        return new(true, created, false, null, null);
    }
}
