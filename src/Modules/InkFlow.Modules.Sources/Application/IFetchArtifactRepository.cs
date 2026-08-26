using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>抓取产物仓储契约。</summary>
public interface IFetchArtifactRepository
{
    Task AddAsync(FetchArtifact artifact, CancellationToken cancellationToken = default);

    /// <summary>某来源章节最近一次的抓取产物；从未抓取过返回 null。</summary>
    Task<FetchArtifact?> GetLatestAsync(
        string sourceId, string externalChapterId, CancellationToken cancellationToken = default);
}
