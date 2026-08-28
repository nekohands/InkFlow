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

public sealed class PrivateBookEntity
{
    public Guid UserId { get; set; }
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Author { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PrivateChapterEntity
{
    public Guid UserId { get; set; }
    public Guid PrivateBookId { get; set; }
    public Guid Id { get; set; }
    public int ChapterIndex { get; set; }
    public string Title { get; set; } = null!;
    public string ContentText { get; set; } = null!;
    public string ContentHash { get; set; } = null!;
    public int ParagraphCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
