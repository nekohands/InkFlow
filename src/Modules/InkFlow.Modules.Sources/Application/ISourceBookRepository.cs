using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>来源侧书目仓储契约。</summary>
public interface ISourceBookRepository
{
    Task AddAsync(SourceBook book, CancellationToken cancellationToken = default);

    /// <summary>按 (sourceId, externalBookId) 定位并加载聚合（含全部章节）。</summary>
    Task<SourceBook?> GetAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default);

    /// <summary>全部已导入书目(不含章节,供追更扫描使用)。</summary>
    Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>写回元数据与新增章节。</summary>
    Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default);
}
