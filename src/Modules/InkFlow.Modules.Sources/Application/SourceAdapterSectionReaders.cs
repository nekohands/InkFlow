using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>共享适配器准备、能力健康检查和成功/失败记录。</summary>
internal sealed class SourceAdapterAccess(
    ISourceAdapterFactory adapterFactory,
    ISourceHealthReader? healthReader,
    ISourceHealthRecorder? healthRecorder)
{
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

    public Task RecordFailureAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken) =>
        healthRecorder is null
            ? Task.CompletedTask
            : healthRecorder.RecordFailureAsync(sourceId, capability, reason, cancellationToken);

    public Task RecordSuccessAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken) =>
        healthRecorder is null
            ? Task.CompletedTask
            : healthRecorder.RecordSuccessAsync(sourceId, capability, cancellationToken);

    private Task<bool> IsAvailableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken) =>
        healthReader is null
            ? Task.FromResult(true)
            : healthReader.IsAvailableAsync(sourceId, capability, cancellationToken);

    private static bool IsExecutionContextValid(
        string sourceId,
        SourceExecutionContext? executionContext) =>
        executionContext is null ||
        string.Equals(executionContext.SourceId, sourceId, StringComparison.Ordinal);
}

/// <summary>目录元数据和目录读取边界。</summary>
internal sealed class SourceCatalogAdapterReader(SourceAdapterAccess access)
{
    public const int MaxTocEntries = SourceRuleExecutionLimits.DefaultMaxResultItems;

    public async Task<SourceAdapterReadResult<SourceBookInfo>> ReadBookInfoAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var preparation = await access
            .PrepareAsync(sourceId, SourceCapability.BookInfo, executionContext, cancellationToken)
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
            await access
                .RecordFailureAsync(
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

        await access
            .RecordSuccessAsync(sourceId, SourceCapability.BookInfo, cancellationToken)
            .ConfigureAwait(false);
        return SourceAdapterReadResult<SourceBookInfo>.Success(info);
    }

    public async Task<SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>> ReadTocAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var preparation = await access
            .PrepareAsync(sourceId, SourceCapability.Toc, executionContext, cancellationToken)
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
            await access
                .RecordFailureAsync(
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
            await access
                .RecordFailureAsync(
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
            await access
                .RecordFailureAsync(
                    sourceId,
                    SourceCapability.Toc,
                    "toc-result-too-large",
                    cancellationToken)
                .ConfigureAwait(false);
            return SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>.Failure(
                $"catalog: Toc result exceeds maximum of {MaxTocEntries} entries.");
        }

        await access
            .RecordSuccessAsync(sourceId, SourceCapability.Toc, cancellationToken)
            .ConfigureAwait(false);
        return SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>.Success(toc);
    }
}

/// <summary>章节正文读取边界。</summary>
internal sealed class SourceContentAdapterReader(SourceAdapterAccess access)
{
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
            await access
                .RecordFailureAsync(
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
            await access
                .RecordFailureAsync(
                    sourceId,
                    SourceCapability.Content,
                    "empty-content",
                    cancellationToken)
                .ConfigureAwait(false);
            return SourceAdapterReadResult<string>.Failure(
                $"content: chapter '{externalChapterId}' returned no content from the source.");
        }

        await access
            .RecordSuccessAsync(sourceId, SourceCapability.Content, cancellationToken)
            .ConfigureAwait(false);
        return SourceAdapterReadResult<string>.Success(rawContent);
    }
}
