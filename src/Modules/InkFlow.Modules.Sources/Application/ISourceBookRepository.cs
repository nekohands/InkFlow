using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public sealed record SourceBookScanCursor(DateTimeOffset CreatedAt, Guid Id);

public sealed record SourceBookPage(
    IReadOnlyList<SourceBook> Books,
    SourceBookScanCursor? NextCursor,
    bool HasMore);

/// <summary>来源侧书目仓储契约。</summary>
public interface ISourceBookRepository
{
    Task AddAsync(SourceBook book, CancellationToken cancellationToken = default);

    /// <summary>按 (sourceId, externalBookId) 定位并加载聚合（含全部章节）。</summary>
    Task<SourceBook?> GetAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default);

    /// <summary>全部已导入书目(不含章节,供追更扫描使用)。</summary>
    Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>按 (CreatedAt, Id) 稳定游标读取有限书目页(供定时追更扫描使用)。</summary>
    Task<SourceBookPage> ListPageAsync(
        SourceBookScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("source book paging is required for scheduled scans.");

    /// <summary>写回元数据与新增章节。</summary>
    Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default);
}
