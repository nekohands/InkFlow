using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public sealed record SourceBookScanCursor(DateTimeOffset CreatedAt, Guid Id);

public sealed record SourceBookPage(
    IReadOnlyList<SourceBook> Books,
    SourceBookScanCursor? NextCursor,
    bool HasMore);

public sealed record SourceChapterLookup(bool BookExists, SourceChapter? Chapter);

public sealed record SourceBookMetadata(string Title, string Author);

/// <summary>来源侧书目仓储契约。</summary>
public interface ISourceBookRepository
{
    Task AddAsync(SourceBook book, CancellationToken cancellationToken = default);

    /// <summary>按 (sourceId, externalBookId) 定位并加载聚合（含全部章节）。</summary>
    Task<SourceBook?> GetAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default);

    /// <summary>按来源书身份读取匹配所需的书名和作者，不加载章节集合。</summary>
    async Task<SourceBookMetadata?> GetMetadataAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default)
    {
        var book = await GetAsync(sourceId, externalBookId, cancellationToken).ConfigureAwait(false);
        return book is null ? null : new SourceBookMetadata(book.Title, book.Author);
    }

    /// <summary>按来源书身份读取按目录顺序排列的外部章节 ID，不加载书籍聚合。</summary>
    async Task<IReadOnlyList<string>> ListChapterIdsAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default)
    {
        var book = await GetAsync(sourceId, externalBookId, cancellationToken).ConfigureAwait(false);
        return book?.Chapters.Select(chapter => chapter.ExternalChapterId).ToList() ?? [];
    }

    /// <summary>按来源书身份读取目标章节；结果保留书不存在与章节不存在的区别。</summary>
    async Task<SourceChapterLookup> GetChapterAsync(
        string sourceId,
        string externalBookId,
        string externalChapterId,
        CancellationToken cancellationToken = default)
    {
        var book = await GetAsync(sourceId, externalBookId, cancellationToken).ConfigureAwait(false);
        return new(
            book is not null,
            book?.Chapters.FirstOrDefault(chapter => chapter.ExternalChapterId == externalChapterId));
    }

    /// <summary>全部已导入书目(不含章节);定时扫描和健康探针必须使用有界查询。</summary>
    Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>按来源读取最早的无章节样本书；没有匹配时返回 null。</summary>
    Task<SourceBook?> FindFirstForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("source-book sample lookup is required for health probes.");

    /// <summary>按 (CreatedAt, Id) 稳定游标读取有限书目页(供定时追更扫描使用)。</summary>
    Task<SourceBookPage> ListPageAsync(
        SourceBookScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("source book paging is required for scheduled scans.");

    /// <summary>写回元数据与新增章节。</summary>
    Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default);
}
