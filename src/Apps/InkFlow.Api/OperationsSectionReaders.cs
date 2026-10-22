using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Api;

internal sealed class OperationsSourceSectionReader(
    ISourceRepository sourceRepository,
    ISourceHealthOperations sourceHealth)
{
    public async Task<OperationsSection<IReadOnlyList<OperationsSourceView>>> ReadAsync(
        IReadOnlySet<string>? allowedSourceIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Source> sources;
        try
        {
            sources = await sourceRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationsSection<IReadOnlyList<OperationsSourceView>>
                .Unavailable("sources_unavailable");
        }

        var visibleSources = allowedSourceIds is null
            ? sources
            : sources.Where(source => allowedSourceIds.Contains(source.Id)).ToList();
        var views = new List<OperationsSourceView>(visibleSources.Count);
        foreach (var source in visibleSources)
        {
            try
            {
                var health = await sourceHealth
                    .ListForSourceAsync(source.Id, cancellationToken)
                    .ConfigureAwait(false);
                views.Add(new OperationsSourceView(
                    source.Id,
                    source.DisplayName,
                    "ready",
                    null,
                    health.Select(SourceHealthEndpointResults.ToResponse).ToList(),
                    source.IsEnabled));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                views.Add(new OperationsSourceView(
                    source.Id,
                    source.DisplayName,
                    "unavailable",
                    "source_health_unavailable",
                    [],
                    source.IsEnabled));
            }
        }

        var unavailableSourceCount = views.Count(view => view.Status != "ready");
        return unavailableSourceCount == 0
            ? OperationsSection<IReadOnlyList<OperationsSourceView>>.Ready(views)
            : OperationsSection<IReadOnlyList<OperationsSourceView>>.Partial(
                views,
                "source_health_unavailable");
    }
}

internal sealed class OperationsCrawlerSectionReader(ICrawlerTaskRepository crawlerTasks)
{
    public async Task<OperationsSection<OperationsCrawlerView>> ReadAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            // 多取一条才能准确表达“还有更多”，但永远只向调用方返回有界数据。
            var rows = await crawlerTasks
                .ListDeadLettersAsync(limit + 1, cancellationToken)
                .ConfigureAwait(false);
            var hasMore = rows.Count > limit;
            var deadLetters = rows
                .Take(limit)
                .Select(deadLetter => new OperationsDeadLetterView(
                    deadLetter.Id,
                    deadLetter.TaskId,
                    deadLetter.SourceId,
                    deadLetter.Reason,
                    deadLetter.AttemptCount,
                    deadLetter.DeadLetteredAt,
                    deadLetter.IsReplayed,
                    deadLetter.ReplayTaskId,
                    deadLetter.ReplayedAt))
                .ToList();

            return OperationsSection<OperationsCrawlerView>.Ready(
                new OperationsCrawlerView(deadLetters.Count, hasMore, deadLetters));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationsSection<OperationsCrawlerView>.Unavailable("crawler_unavailable");
        }
    }
}

internal sealed class OperationsConsistencySectionReader(IConsistencyCheckService consistency)
{
    public async Task<OperationsSection<ConsistencyCheckReport>> ReadAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return OperationsSection<ConsistencyCheckReport>.Ready(
                await consistency.CheckAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationsSection<ConsistencyCheckReport>
                .Unavailable("consistency_unavailable");
        }
    }
}
