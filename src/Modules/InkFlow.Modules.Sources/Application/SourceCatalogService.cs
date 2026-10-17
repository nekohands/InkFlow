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
    private readonly SourceAdapterReader sourceReader =
        new(adapterFactory, healthReader, healthRecorder);

    public const int MaxTocEntries = SourceAdapterReader.MaxTocEntries;

    /// <summary>
    /// 抓取并导入一本书的元数据。已存在的书更新元数据，否则创建。
    /// </summary>
    public async Task<ImportOutcome> ImportBookInfoAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default,
        SourceExecutionContext? executionContext = null)
    {
        var read = await sourceReader
            .ReadBookInfoAsync(sourceId, externalBookId, cancellationToken, executionContext)
            .ConfigureAwait(false);
        if (!read.IsSuccess)
        {
            return ImportOutcome.Fail([read.Error!]);
        }

        var existing = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var created = SourceBook.Create(
                sourceId,
                externalBookId,
                read.Value!.Title,
                read.Value.Author,
                clock.GetUtcNow());
            await sourceBookRepository.AddAsync(created, cancellationToken).ConfigureAwait(false);
            return ImportOutcome.Ok(created);
        }

        existing.UpdateMetadata(read.Value!.Title, read.Value.Author, clock.GetUtcNow());
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
        var read = await sourceReader
            .ReadTocAsync(sourceId, externalBookId, cancellationToken, executionContext)
            .ConfigureAwait(false);
        if (!read.IsSuccess)
        {
            return ImportOutcome.Fail([read.Error!]);
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
            read.Value!.Select(c => (c.ExternalChapterId, c.Title)),
            clock.GetUtcNow());
        await sourceBookRepository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
        return ImportOutcome.Ok(existing);
    }
}
