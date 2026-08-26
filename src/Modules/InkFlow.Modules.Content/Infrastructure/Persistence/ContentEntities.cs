namespace InkFlow.Modules.Content.Infrastructure.Persistence;

public static class ContentSchema
{
    public const string Name = "content";
}

/// <summary>content_versions 表实体。</summary>
public sealed class ContentVersionEntity
{
    public Guid Id { get; set; }
    public Guid CanonicalBookId { get; set; }
    public Guid CanonicalChapterId { get; set; }
    public string SourceId { get; set; } = null!;
    public string CanonicalHash { get; set; } = null!;
    public string CanonicalText { get; set; } = null!;
    public int ParagraphCount { get; set; }
    public int QualityScore { get; set; }
    public bool IsCurrent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
