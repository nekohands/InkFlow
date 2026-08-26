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
    TimeProvider clock)
{
    /// <summary>
    /// 抓取并导入一本书的元数据。已存在的书更新元数据，否则创建。
    /// </summary>
    public async Task<ImportOutcome> ImportBookInfoAsync(
        string sourceId, string externalBookId, CancellationToken cancellationToken = default)
    {
        var adapter = await RequireAdapterAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (adapter is null)
        {
            return ImportOutcome.Fail([$"source '{sourceId}' does not exist or has no adapter."]);
        }

        var info = await adapter
            .GetBookInfoAsync(externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (info is null)
        {
            return ImportOutcome.Fail(
                [$"catalog: book '{sourceId}/{externalBookId}' was not found at the source."]);
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
        string sourceId, string externalBookId, CancellationToken cancellationToken = default)
    {
        var adapter = await RequireAdapterAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (adapter is null)
        {
            return ImportOutcome.Fail([$"source '{sourceId}' does not exist or has no adapter."]);
        }

        var toc = await adapter
            .GetTableOfContentsAsync(externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (toc.Count == 0)
        {
            return ImportOutcome.Fail(
                [$"catalog: no chapters returned for book '{sourceId}/{externalBookId}'."]);
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

    private Task<ISourceAdapter?> RequireAdapterAsync(string sourceId, CancellationToken cancellationToken) =>
        adapterFactory.GetAdapterAsync(sourceId, cancellationToken);
}
