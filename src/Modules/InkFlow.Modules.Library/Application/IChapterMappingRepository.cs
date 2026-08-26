using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

/// <summary>章节映射仓储契约。</summary>
public interface IChapterMappingRepository
{
    Task AddAsync(ChapterMapping mapping, CancellationToken cancellationToken = default);

    Task<ChapterMapping?> FindAsync(
        string sourceId, string externalChapterId, CancellationToken cancellationToken = default);
}
