namespace InkFlow.Modules.Library.Domain;

/// <summary>
/// 私有书籍章节。章节和正文均绑定 PrivateBook，不承担公共 ChapterId 语义。
/// </summary>
public sealed class PrivateChapter
{
    public const int MaxTitleLength = 512;

    private PrivateChapter() { }

    public Guid UserId { get; private set; }
    public Guid PrivateBookId { get; private set; }
    public Guid Id { get; private set; }
    public int Index { get; private set; }
    public string Title { get; private set; } = null!;
    public PrivateContentDocument Content { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public int ParagraphCount => Content.Paragraphs.Count;
    public string ContentText => Content.CanonicalText;
    public string ContentHash => Content.ContentHash;

    public static PrivateChapter Create(
        Guid userId,
        Guid privateBookId,
        int index,
        string title,
        PrivateContentDocument content,
        DateTimeOffset now)
    {
        ValidateIdentity(userId, privateBookId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        ArgumentNullException.ThrowIfNull(content);

        return new PrivateChapter
        {
            UserId = userId,
            PrivateBookId = privateBookId,
            Id = Guid.CreateVersion7(),
            Index = index,
            Title = NormalizeTitle(title),
            Content = content,
            CreatedAt = now,
        };
    }

    public static PrivateChapter Rehydrate(
        Guid userId,
        Guid privateBookId,
        Guid id,
        int index,
        string title,
        string contentText,
        string contentHash,
        int paragraphCount,
        DateTimeOffset createdAt)
    {
        ValidateIdentity(userId, privateBookId);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("private chapter id must not be empty.", nameof(id));
        }

        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return new PrivateChapter
        {
            UserId = userId,
            PrivateBookId = privateBookId,
            Id = id,
            Index = index,
            Title = NormalizeTitle(title),
            Content = PrivateContentDocument.Rehydrate(
                contentText, contentHash, paragraphCount),
            CreatedAt = createdAt,
        };
    }

    private static void ValidateIdentity(Guid userId, Guid privateBookId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("private chapter owner must not be empty.", nameof(userId));
        }

        if (privateBookId == Guid.Empty)
        {
            throw new ArgumentException(
                "private chapter book id must not be empty.", nameof(privateBookId));
        }
    }

    private static string NormalizeTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("private chapter title must not be empty.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > MaxTitleLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"private chapter title must be at most {MaxTitleLength} characters and contain no control characters.",
                nameof(value));
        }

        return normalized;
    }
}
