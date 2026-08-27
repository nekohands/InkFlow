using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>内容版本仓储契约。</summary>
public interface IContentVersionRepository
{
    Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default);

    /// <summary>查找同章节下规范化内容完全一致的版本;不存在返回 null。</summary>
    Task<ContentVersion?> FindByHashAsync(
        Guid canonicalChapterId, string canonicalHash, CancellationToken cancellationToken = default);

    /// <summary>章节的全部版本(用于选优)。</summary>
    Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
        Guid canonicalChapterId, CancellationToken cancellationToken = default);

    /// <summary>章节的当前(IsCurrent)版本;尚未发布内容时返回 null。</summary>
    Task<ContentVersion?> GetCurrentForChapterAsync(
        Guid canonicalChapterId, CancellationToken cancellationToken = default);

    /// <summary>只读取当前版本关联的正典书 ID，用于正文加载前的可见性门控。</summary>
    Task<Guid?> GetCurrentCanonicalBookIdAsync(
        Guid canonicalChapterId, CancellationToken cancellationToken = default);

    /// <summary>原子地把某章节的当前版本切换为指定版本。</summary>
    Task SetCurrentAsync(Guid chapterId, Guid versionId, CancellationToken cancellationToken = default);
}
