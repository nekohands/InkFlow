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
