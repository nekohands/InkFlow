using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public sealed record ImportOutcome(
    bool IsSuccess,
    SourceBook? Book,
    IReadOnlyList<string> Errors)
{
    public static ImportOutcome Ok(SourceBook book) => new(true, book, []);
    public static ImportOutcome Fail(IReadOnlyList<string> errors) => new(false, null, errors);
}

/// <summary>
/// 来源目录服务:通过书源兼容层(<see cref="ISourceAdapter"/>)执行上层目录操作——
/// 导入书目元数据、同步目录并落库。上层不感知站点差异。
/// </summary>
public sealed class SourceCatalogService(
    ISourceAdapterFactory adapterFactory,
    ISourceBookRepository sourceBookRepository,
    TimeProvider clock,
    ISourceHealthReader? healthReader = null,
    ISourceHealthRecorder? healthRecorder = null)
{
    /// <summary>
    /// 抓取并导入一本书的元数据。已存在的书更新元数据，否则创建。
    /// </summary>
    public async Task<ImportOutcome> ImportBookInfoAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default,
        SourceExecutionContext? executionContext = null)
    {
        if (!IsExecutionContextValid(sourceId, executionContext))
        {
            return ImportOutcome.Fail(["source: execution context is invalid."]);
        }

        if (!await IsAvailableAsync(sourceId, SourceCapability.BookInfo, cancellationToken)
                .ConfigureAwait(false))
        {
            return ImportOutcome.Fail(
                [$"source '{sourceId}' capability BookInfo is unavailable; retry later."]);
        }

        var adapter = await RequireAdapterAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (adapter is null)
        {
            return ImportOutcome.Fail([$"source '{sourceId}' does not exist or has no adapter."]);
        }

        SourceBookInfo? info;
        try
        {
            info = await adapter
                .GetBookInfoAsync(externalBookId, cancellationToken, executionContext)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (healthRecorder is not null)
            {
                await healthRecorder.RecordFailureAsync(
                    sourceId,
                    SourceCapability.BookInfo,
                    "adapter-exception",
                    cancellationToken).ConfigureAwait(false);
            }

            return ImportOutcome.Fail(["catalog: source request failed for BookInfo."]);
        }

        if (info is null)
        {
            return ImportOutcome.Fail(
                [$"catalog: book '{sourceId}/{externalBookId}' was not found at the source."]);
        }

        if (healthRecorder is not null)
        {
            await healthRecorder.RecordSuccessAsync(
                sourceId, SourceCapability.BookInfo, cancellationToken).ConfigureAwait(false);
        }

        var existing = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var created = SourceBook.Create(sourceId, externalBookId, info.Title, info.Author, clock.GetUtcNow());
            await sourceBookRepository.AddAsync(created, cancellationToken).ConfigureAwait(false);
            return ImportOutcome.Ok(created);
        }

        existing.UpdateMetadata(info.Title, info.Author, clock.GetUtcNow());
        await sourceBookRepository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
        return ImportOutcome.Ok(existing);
    }

    /// <summary>同步一本书的目录(按外部章节 ID 幂等追加)。</summary>
    public async Task<ImportOutcome> SyncChaptersAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default,
        SourceExecutionContext? executionContext = null)
    {
        if (!IsExecutionContextValid(sourceId, executionContext))
        {
            return ImportOutcome.Fail(["source: execution context is invalid."]);
        }

        if (!await IsAvailableAsync(sourceId, SourceCapability.Toc, cancellationToken)
                .ConfigureAwait(false))
        {
            return ImportOutcome.Fail(
                [$"source '{sourceId}' capability Toc is unavailable; retry later."]);
        }

        var adapter = await RequireAdapterAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (adapter is null)
        {
            return ImportOutcome.Fail([$"source '{sourceId}' does not exist or has no adapter."]);
        }

        IReadOnlyList<SourceTocEntry> toc;
        try
        {
            toc = await adapter
                .GetTableOfContentsAsync(externalBookId, cancellationToken, executionContext)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (healthRecorder is not null)
            {
                await healthRecorder.RecordFailureAsync(
                    sourceId,
                    SourceCapability.Toc,
                    "adapter-exception",
                    cancellationToken).ConfigureAwait(false);
            }

            return ImportOutcome.Fail(["catalog: source request failed for Toc."]);
        }

        if (toc.Count == 0)
        {
            if (healthRecorder is not null)
            {
                await healthRecorder.RecordFailureAsync(
                    sourceId,
                    SourceCapability.Toc,
                    "empty-toc",
                    cancellationToken).ConfigureAwait(false);
            }

            return ImportOutcome.Fail(
                [$"catalog: no chapters returned for book '{sourceId}/{externalBookId}'."]);
        }

        if (healthRecorder is not null)
        {
            await healthRecorder.RecordSuccessAsync(
                sourceId, SourceCapability.Toc, cancellationToken).ConfigureAwait(false);
        }

        var existing = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return ImportOutcome.Fail(
                [$"catalog: book '{sourceId}/{externalBookId}' must be imported (BookInfo) before syncing chapters."]);
        }

        existing.SyncChapters(
            toc.Select(c => (c.ExternalChapterId, c.Title)), clock.GetUtcNow());
        await sourceBookRepository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
        return ImportOutcome.Ok(existing);
    }

    private Task<bool> IsAvailableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken) =>
        healthReader is null
            ? Task.FromResult(true)
            : healthReader.IsAvailableAsync(sourceId, capability, cancellationToken);

    private Task<ISourceAdapter?> RequireAdapterAsync(string sourceId, CancellationToken cancellationToken) =>
        adapterFactory.GetAdapterAsync(sourceId, cancellationToken);

    private static bool IsExecutionContextValid(
        string sourceId,
        SourceExecutionContext? executionContext) =>
        executionContext is null ||
        string.Equals(executionContext.SourceId, sourceId, StringComparison.Ordinal);
}
