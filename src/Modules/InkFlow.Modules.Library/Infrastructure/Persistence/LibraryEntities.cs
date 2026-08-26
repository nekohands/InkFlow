namespace InkFlow.Modules.Library.Infrastructure.Persistence;

public sealed class CanonicalBookEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Author { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CanonicalChapterEntity
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public int ChapterIndex { get; set; }
    public string Title { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
