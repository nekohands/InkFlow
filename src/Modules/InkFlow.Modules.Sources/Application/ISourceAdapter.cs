namespace InkFlow.Modules.Sources.Application;

/// <summary>搜索结果条目。</summary>
public sealed record SourceSearchResult(string ExternalBookId, string Title, string Author);

/// <summary>书目元数据。</summary>
public sealed record SourceBookInfo(string Title, string Author);

/// <summary>目录条目。</summary>
public sealed record SourceTocEntry(string ExternalChapterId, int Index, string Title);

/// <summary>
/// <b>书源兼容层统一契约</b>:所有来源(规则驱动 DSL 或定制代码)都实现此接口,
/// 上层操作(目录导入、追更调度、正文抓取)仅面向该抽象,不感知站点差异。
///
/// 扩展方式:
/// 1. 规则型来源——在 sources 表登记一条含 RuleDsl 的 Source 记录即可,零代码
///    (由 <see cref="RuleBasedSourceAdapter"/> 解释执行);
/// 2. 定制代码来源——实现本接口(处理特殊编码/签名/登录等),在适配器工厂注册。
/// </summary>
public interface ISourceAdapter
{
    /// <summary>该适配器服务的来源标识。</summary>
    string SourceId { get; }

    /// <summary>按关键词搜索书目。</summary>
    Task<IReadOnlyList<SourceSearchResult>> SearchAsync(string keyword, CancellationToken cancellationToken = default);

    /// <summary>获取书目元数据;不存在返回 null。</summary>
    Task<SourceBookInfo?> GetBookInfoAsync(string externalBookId, CancellationToken cancellationToken = default);

    /// <summary>获取完整目录(按阅读顺序)。</summary>
    Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(string externalBookId, CancellationToken cancellationToken = default);

    /// <summary>获取章节原始正文;无内容返回 null。</summary>
    Task<string?> GetChapterContentAsync(string externalChapterId, CancellationToken cancellationToken = default);
}
