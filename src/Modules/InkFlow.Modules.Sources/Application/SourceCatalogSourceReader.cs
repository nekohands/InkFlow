using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed record SourceCatalogReadResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;

    public static SourceCatalogReadResult<T> Success(T value) => new(value, null);

    public static SourceCatalogReadResult<T> Failure(string error) => new(default, error);
}

/// <summary>
/// 来源读取边界：校验执行上下文、检查能力健康、调用适配器并记录结果。
/// 书目聚合的创建与持久化仍由 SourceCatalogService 编排。
/// </summary>
internal sealed class SourceCatalogSourceReader(
    ISourceAdapterFactory adapterFactory,
    ISourceHealthReader? healthReader,
    ISourceHealthRecorder? healthRecorder)
{
    public const int MaxTocEntries = SourceRuleExecutionLimits.DefaultMaxResultItems;

    public async Task<SourceCatalogReadResult<SourceBookInfo>> ReadBookInfoAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var preparation = await PrepareAsync(
                sourceId,
                SourceCapability.BookInfo,
                executionContext,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
        {
            return SourceCatalogReadResult<SourceBookInfo>.Failure(preparation.Error!);
        }

        SourceBookInfo? info;
        try
        {
            info = await preparation.Adapter!
                .GetBookInfoAsync(externalBookId, cancellationToken, executionContext)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordFailureAsync(
                    sourceId,
                    SourceCapability.BookInfo,
                    "adapter-exception",
                    cancellationToken)
                .ConfigureAwait(false);
            return SourceCatalogReadResult<SourceBookInfo>.Failure(
                "catalog: source request failed for BookInfo.");
        }

        if (info is null)
        {
            return SourceCatalogReadResult<SourceBookInfo>.Failure(
                $"catalog: book '{sourceId}/{externalBookId}' was not found at the source.");
        }

        await RecordSuccessAsync(sourceId, SourceCapability.BookInfo, cancellationToken)
            .ConfigureAwait(false);
        return SourceCatalogReadResult<SourceBookInfo>.Success(info);
    }

    public async Task<SourceCatalogReadResult<IReadOnlyList<SourceTocEntry>>> ReadTocAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var preparation = await PrepareAsync(
                sourceId,
                SourceCapability.Toc,
                executionContext,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
        {
            return SourceCatalogReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
                preparation.Error!);
        }

        IReadOnlyList<SourceTocEntry> toc;
        try
        {
            toc = await preparation.Adapter!
                .GetTableOfContentsAsync(externalBookId, cancellationToken, executionContext)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordFailureAsync(
                    sourceId,
                    SourceCapability.Toc,
                    "adapter-exception",
                    cancellationToken)
                .ConfigureAwait(false);
            return SourceCatalogReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
                "catalog: source request failed for Toc.");
        }

        if (toc.Count == 0)
        {
            await RecordFailureAsync(
                    sourceId,
                    SourceCapability.Toc,
                    "empty-toc",
                    cancellationToken)
                .ConfigureAwait(false);
            return SourceCatalogReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
                $"catalog: no chapters returned for book '{sourceId}/{externalBookId}'.");
        }

        if (toc.Count > MaxTocEntries)
        {
            await RecordFailureAsync(
                    sourceId,
                    SourceCapability.Toc,
                    "toc-result-too-large",
                    cancellationToken)
                .ConfigureAwait(false);
            return SourceCatalogReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
                $"catalog: Toc result exceeds maximum of {MaxTocEntries} entries.");
        }

        await RecordSuccessAsync(sourceId, SourceCapability.Toc, cancellationToken)
            .ConfigureAwait(false);
        return SourceCatalogReadResult<IReadOnlyList<SourceTocEntry>>.Success(toc);
    }

    private async Task<SourceCatalogPreparation> PrepareAsync(
        string sourceId,
        SourceCapability capability,
        SourceExecutionContext? executionContext,
        CancellationToken cancellationToken)
    {
        if (!IsExecutionContextValid(sourceId, executionContext))
        {
            return SourceCatalogPreparation.Failure("source: execution context is invalid.");
        }

        if (!await IsAvailableAsync(sourceId, capability, cancellationToken).ConfigureAwait(false))
        {
            return SourceCatalogPreparation.Failure(
                $"source '{sourceId}' capability {capability} is unavailable; retry later.");
        }

        var adapter = await adapterFactory.GetAdapterAsync(sourceId, cancellationToken)
            .ConfigureAwait(false);
        return adapter is null
            ? SourceCatalogPreparation.Failure(
                $"source '{sourceId}' does not exist or has no adapter.")
            : SourceCatalogPreparation.Success(adapter);
    }

    private Task<bool> IsAvailableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken) =>
        healthReader is null
            ? Task.FromResult(true)
            : healthReader.IsAvailableAsync(sourceId, capability, cancellationToken);

    private Task RecordFailureAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken) =>
        healthRecorder is null
            ? Task.CompletedTask
            : healthRecorder.RecordFailureAsync(sourceId, capability, reason, cancellationToken);

    private Task RecordSuccessAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken) =>
        healthRecorder is null
            ? Task.CompletedTask
            : healthRecorder.RecordSuccessAsync(sourceId, capability, cancellationToken);

    private static bool IsExecutionContextValid(
        string sourceId,
        SourceExecutionContext? executionContext) =>
        executionContext is null ||
        string.Equals(executionContext.SourceId, sourceId, StringComparison.Ordinal);

    private sealed record SourceCatalogPreparation(ISourceAdapter? Adapter, string? Error)
    {
        public bool IsSuccess => Error is null;

        public static SourceCatalogPreparation Success(ISourceAdapter adapter) => new(adapter, null);

        public static SourceCatalogPreparation Failure(string error) => new(null, error);
    }
}
