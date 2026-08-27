using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Api;

/// <summary>
/// Operations/Repair 只读区块。区块失败时返回稳定错误码，不把基础设施异常泄漏给调用方。
/// </summary>
public sealed record OperationsSection<T>(
    string Status,
    T? Data,
    string? Error)
{
    public static OperationsSection<T> Ready(T data) => new("ready", data, null);

    public static OperationsSection<T> Partial(T data, string error) =>
        new("partial", data, error);

    public static OperationsSection<T> Unavailable(string error) =>
        new("unavailable", default, error);
}

public sealed record OperationsSourceView(
    string SourceId,
    string DisplayName,
    string Status,
    string? Error,
    IReadOnlyList<SourceHealthResponse> Capabilities);

public sealed record OperationsDeadLetterView(
    Guid Id,
    Guid TaskId,
    string SourceId,
    string Reason,
    int AttemptCount,
    DateTimeOffset DeadLetteredAt,
    bool IsReplayed,
    Guid? ReplayTaskId,
    DateTimeOffset? ReplayedAt);

public sealed record OperationsCrawlerView(
    int ReturnedDeadLetterCount,
    bool HasMoreDeadLetters,
    IReadOnlyList<OperationsDeadLetterView> DeadLetters);

public sealed record OperationsCenterResponse(
    DateTimeOffset GeneratedAt,
    string Status,
    OperationsSection<IReadOnlyList<OperationsSourceView>> Sources,
    OperationsSection<OperationsCrawlerView> Crawler,
    OperationsSection<ConsistencyCheckReport> Consistency);

/// <summary>
/// Operations Center 的深接口：调用方只需一次读取即可获得受限、可解释的运维快照。
/// 实现负责区块隔离、上限和领域结果到管理读模型的映射。
/// </summary>
public interface IOperationsCenterReader
{
    Task<OperationsCenterResponse> ReadAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class OperationsCenterReader(
    ISourceRepository sourceRepository,
    ISourceHealthOperations sourceHealth,
    ICrawlerTaskRepository crawlerTasks,
    IConsistencyCheckService consistency,
    TimeProvider clock) : IOperationsCenterReader
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public async Task<OperationsCenterResponse> ReadAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, MaxLimit);
        var sources = await ReadSourcesAsync(cancellationToken).ConfigureAwait(false);
        var crawler = await ReadCrawlerAsync(boundedLimit, cancellationToken).ConfigureAwait(false);
        var consistencyReport = await ReadConsistencyAsync(cancellationToken).ConfigureAwait(false);
        var status = sources.Status == "ready" &&
                     crawler.Status == "ready" &&
                     consistencyReport.Status == "ready"
            ? "ready"
            : "partial";

        return new OperationsCenterResponse(
            clock.GetUtcNow(),
            status,
            sources,
            crawler,
            consistencyReport);
    }

    private async Task<OperationsSection<IReadOnlyList<OperationsSourceView>>> ReadSourcesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<InkFlow.Modules.Sources.Domain.Source> sources;
        try
        {
            sources = await sourceRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationsSection<IReadOnlyList<OperationsSourceView>>
                .Unavailable("sources_unavailable");
        }

        var views = new List<OperationsSourceView>(sources.Count);
        foreach (var source in sources)
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
                    health.Select(SourceHealthEndpointResults.ToResponse).ToList()));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                views.Add(new OperationsSourceView(
                    source.Id,
                    source.DisplayName,
                    "unavailable",
                    "source_health_unavailable",
                    []));
            }
        }

        var unavailableSourceCount = views.Count(view => view.Status != "ready");
        return unavailableSourceCount == 0
            ? OperationsSection<IReadOnlyList<OperationsSourceView>>.Ready(views)
            : OperationsSection<IReadOnlyList<OperationsSourceView>>.Partial(
                views,
                "source_health_unavailable");
    }

    private async Task<OperationsSection<OperationsCrawlerView>> ReadCrawlerAsync(
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

    private async Task<OperationsSection<ConsistencyCheckReport>> ReadConsistencyAsync(
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
