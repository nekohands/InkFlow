namespace InkFlow.Modules.Library.Infrastructure.Persistence;

/// <summary>chapter_mappings 表实体。</summary>
public sealed class ChapterMappingEntity
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = null!;
    public string ExternalChapterId { get; set; } = null!;
    public Guid SourceChapterId { get; set; }
    public Guid CanonicalBookId { get; set; }
    public Guid CanonicalChapterId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
