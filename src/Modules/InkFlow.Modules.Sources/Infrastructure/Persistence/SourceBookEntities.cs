namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

/// <summary>source_books / source_chapters 表实体。</summary>
public sealed class SourceBookEntity
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = null!;
    public string ExternalBookId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Author { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SourceChapterEntity
{
    public Guid Id { get; set; }
    public Guid SourceBookId { get; set; }
    public string ExternalChapterId { get; set; } = null!;
    public int ChapterIndex { get; set; }
    public string Title { get; set; } = null!;
}
