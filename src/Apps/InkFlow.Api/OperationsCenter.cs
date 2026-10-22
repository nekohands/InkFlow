using InkFlow.Modules.Operations.Application;
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
    IReadOnlyList<SourceHealthResponse> Capabilities,
    bool IsEnabled = true);

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

public sealed record OperationsSourceStatusResponse(
    DateTimeOffset GeneratedAt,
    string Status,
    OperationsSection<IReadOnlyList<OperationsSourceView>> Sources);

/// <summary>
/// Operations Center 的深接口：调用方只需一次读取即可获得受限、可解释的运维快照。
/// 实现负责区块隔离、上限和领域结果到管理读模型的映射。
/// </summary>
public interface IOperationsCenterReader
{
    Task<OperationsCenterResponse> ReadAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<OperationsCenterResponse> ReadForSourcesAsync(
        int limit,
        IReadOnlySet<string> allowedSourceIds,
        CancellationToken cancellationToken = default);
    Task<OperationsSourceStatusResponse> ReadSourcesOnlyAsync(
        CancellationToken cancellationToken = default);
}

public sealed class OperationsCenterReader(
    ISourceRepository sourceRepository,
    ISourceHealthOperations sourceHealth,
    ICrawlerTaskRepository crawlerTasks,
    IConsistencyCheckService consistency,
    TimeProvider clock) : IOperationsCenterReader
{
    private readonly OperationsSourceSectionReader sourceReader =
        new(sourceRepository, sourceHealth);
    private readonly OperationsCrawlerSectionReader crawlerReader =
        new(crawlerTasks);
    private readonly OperationsConsistencySectionReader consistencyReader =
        new(consistency);

    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public async Task<OperationsCenterResponse> ReadAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await ReadCoreAsync(limit, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationsCenterResponse> ReadForSourcesAsync(
        int limit,
        IReadOnlySet<string> allowedSourceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allowedSourceIds);
        return await ReadCoreAsync(limit, allowedSourceIds, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OperationsSourceStatusResponse> ReadSourcesOnlyAsync(
        CancellationToken cancellationToken = default)
    {
        var sources = await sourceReader.ReadAsync(null, cancellationToken).ConfigureAwait(false);
        return new(
            clock.GetUtcNow(),
            sources.Status,
            sources);
    }

    private async Task<OperationsCenterResponse> ReadCoreAsync(
        int limit,
        IReadOnlySet<string>? allowedSourceIds,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, MaxLimit);
        var sources = await sourceReader.ReadAsync(allowedSourceIds, cancellationToken)
            .ConfigureAwait(false);
        var crawler = await crawlerReader.ReadAsync(boundedLimit, cancellationToken)
            .ConfigureAwait(false);
        var consistencyReport = await consistencyReader.ReadAsync(cancellationToken)
            .ConfigureAwait(false);
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
}
