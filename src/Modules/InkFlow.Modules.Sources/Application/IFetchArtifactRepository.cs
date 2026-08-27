using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>抓取产物仓储契约。</summary>
public interface IFetchArtifactRepository
{
    Task AddAsync(FetchArtifact artifact, CancellationToken cancellationToken = default);

    /// <summary>某来源章节最近一次的抓取产物；从未抓取过返回 null。</summary>
    Task<FetchArtifact?> GetLatestAsync(
        string sourceId, string externalChapterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 给定章节集合,返回其中已有至少一个抓取产物的 ExternalChapterId 集合
    /// ("该来源已成功抓取过正文"的判据)。用于追更联动时批量甄别未抓取的新章。
    /// </summary>
    Task<IReadOnlySet<string>> ListFetchedExternalChapterIdsAsync(
        string sourceId,
        IEnumerable<string> externalChapterIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 给定章节集合,返回其中在 <paramref name="since"/> 之后仍产生过产物
    /// (含复检落行的相同哈希记录)的 ExternalChapterId 集合。用于修订重扫的
    /// 保鲜判定:不在结果内的已抓章节即视为过期,应按节流策略重新抓取。
    /// </summary>
    Task<IReadOnlySet<string>> ListRecentlyFetchedExternalChapterIdsAsync(
        string sourceId,
        IEnumerable<string> externalChapterIds,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);
}
