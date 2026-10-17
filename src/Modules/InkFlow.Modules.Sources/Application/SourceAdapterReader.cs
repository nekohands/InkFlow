using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed record SourceAdapterReadResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;

    public static SourceAdapterReadResult<T> Success(T value) => new(value, null);

    public static SourceAdapterReadResult<T> Failure(string error) => new(default, error);
}

internal sealed record SourceAdapterPreparation(ISourceAdapter? Adapter, string? Error)
{
    public bool IsSuccess => Error is null;

    public static SourceAdapterPreparation Success(ISourceAdapter adapter) => new(adapter, null);

    public static SourceAdapterPreparation Failure(string error) => new(null, error);
}

/// <summary>
/// 来源适配器读取边界：校验执行上下文、检查能力健康、解析适配器并记录请求结果。
/// 目录聚合和抓取产物的校验与持久化由各自的应用服务编排。
/// </summary>
internal sealed class SourceAdapterReader(
    ISourceAdapterFactory adapterFactory,
    ISourceHealthReader? healthReader,
    ISourceHealthRecorder? healthRecorder)
{
    public const int MaxTocEntries = SourceRuleExecutionLimits.DefaultMaxResultItems;

    public async Task<SourceAdapterReadResult<SourceBookInfo>> ReadBookInfoAsync(
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
            return SourceAdapterReadResult<SourceBookInfo>.Failure(preparation.Error!);
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
            return SourceAdapterReadResult<SourceBookInfo>.Failure(
                "catalog: source request failed for BookInfo.");
        }

        if (info is null)
        {
            return SourceAdapterReadResult<SourceBookInfo>.Failure(
                $"catalog: book '{sourceId}/{externalBookId}' was not found at the source.");
        }

        await RecordSuccessAsync(sourceId, SourceCapability.BookInfo, cancellationToken)
            .ConfigureAwait(false);
        return SourceAdapterReadResult<SourceBookInfo>.Success(info);
    }

    public async Task<SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>> ReadTocAsync(
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
            return SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
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
            return SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
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
            return SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
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
            return SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
                $"catalog: Toc result exceeds maximum of {MaxTocEntries} entries.");
        }

        await RecordSuccessAsync(sourceId, SourceCapability.Toc, cancellationToken)
            .ConfigureAwait(false);
        return SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>.Success(toc);
    }

    public async Task<SourceAdapterReadResult<string>> ReadChapterContentAsync(
        SourceAdapterPreparation preparation,
        string sourceId,
        string externalChapterId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        if (!preparation.IsSuccess)
        {
            return SourceAdapterReadResult<string>.Failure(preparation.Error!);
        }

        string? rawContent;
        try
        {
            rawContent = await preparation.Adapter!
                .GetChapterContentAsync(externalChapterId, cancellationToken, executionContext)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordFailureAsync(
                    sourceId,
                    SourceCapability.Content,
                    "adapter-exception",
                    cancellationToken)
                .ConfigureAwait(false);
            return SourceAdapterReadResult<string>.Failure(
                "content: source request failed for Content.");
        }

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            await RecordFailureAsync(
                    sourceId,
                    SourceCapability.Content,
                    "empty-content",
                    cancellationToken)
                .ConfigureAwait(false);
            return SourceAdapterReadResult<string>.Failure(
                $"content: chapter '{externalChapterId}' returned no content from the source.");
        }

        await RecordSuccessAsync(sourceId, SourceCapability.Content, cancellationToken)
            .ConfigureAwait(false);
        return SourceAdapterReadResult<string>.Success(rawContent);
    }

    public async Task<SourceAdapterPreparation> PrepareAsync(
        string sourceId,
        SourceCapability capability,
        SourceExecutionContext? executionContext,
        CancellationToken cancellationToken)
    {
        if (!IsExecutionContextValid(sourceId, executionContext))
        {
            return SourceAdapterPreparation.Failure("source: execution context is invalid.");
        }

        if (!await IsAvailableAsync(sourceId, capability, cancellationToken).ConfigureAwait(false))
        {
            return SourceAdapterPreparation.Failure(
                $"source '{sourceId}' capability {capability} is unavailable; retry later.");
        }

        var adapter = await adapterFactory.GetAdapterAsync(sourceId, cancellationToken)
            .ConfigureAwait(false);
        return adapter is null
            ? SourceAdapterPreparation.Failure(
                $"source '{sourceId}' does not exist or has no adapter.")
            : SourceAdapterPreparation.Success(adapter);
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
}
