namespace InkFlow.Modules.Reading.Infrastructure.Persistence;

public static class ReadingSchema
{
    public const string Name = "reading";
}

public sealed class BookshelfEntryEntity
{
    public Guid UserId { get; set; }
    public Guid CanonicalBookId { get; set; }
    public int Status { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReadingProgressEntity
{
    public Guid UserId { get; set; }
    public Guid CanonicalBookId { get; set; }
    public Guid CanonicalChapterId { get; set; }
    public int ParagraphIndex { get; set; }
    public int ProgressPercent { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReadingHistoryEntryEntity
{
    public Guid UserId { get; set; }
    public Guid CanonicalBookId { get; set; }
    public Guid CanonicalChapterId { get; set; }
    public DateTimeOffset FirstReadAt { get; set; }
    public DateTimeOffset LastReadAt { get; set; }
}

public sealed class ReaderPreferenceEntity
{
    public Guid UserId { get; set; }
    public int FontSizePercent { get; set; }
    public int LineHeightPercent { get; set; }
    public int Theme { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
