namespace InkFlow.Modules.Reading.Domain;

public enum ShelfStatus
{
    Reading = 1,
    WantToRead = 2,
    Paused = 3,
    Completed = 4,
}

public enum ReaderTheme
{
    System = 1,
    Light = 2,
    Sepia = 3,
    Dark = 4,
}

/// <summary>
/// 用户书架条目。CanonicalBookId 是跨模块引用，不在 Reading schema 中复制书籍主数据。
/// </summary>
public sealed class BookshelfEntry
{
    public Guid UserId { get; private set; }
    public Guid CanonicalBookId { get; private set; }
    public ShelfStatus Status { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private BookshelfEntry() { }

    public static BookshelfEntry Create(
        Guid userId,
        Guid canonicalBookId,
        ShelfStatus status,
        DateTimeOffset now)
    {
        ReadingStateValidation.ValidateIdentity(userId, canonicalBookId);
        ReadingStateValidation.ValidateShelfStatus(status);

        return new BookshelfEntry
        {
            UserId = userId,
            CanonicalBookId = canonicalBookId,
            Status = status,
            AddedAt = now,
            UpdatedAt = now,
        };
    }

    public static BookshelfEntry Rehydrate(
        Guid userId,
        Guid canonicalBookId,
        ShelfStatus status,
        DateTimeOffset addedAt,
        DateTimeOffset updatedAt) =>
        new()
        {
            UserId = userId,
            CanonicalBookId = canonicalBookId,
            Status = status,
            AddedAt = addedAt,
            UpdatedAt = updatedAt,
        };

    public void ChangeStatus(ShelfStatus status, DateTimeOffset now)
    {
        ReadingStateValidation.ValidateShelfStatus(status);
        Status = status;
        UpdatedAt = now;
    }
}

/// <summary>
/// 每个用户/书籍一条当前阅读位置。章节 ID 与位置均指向稳定 Canonical 身份。
/// </summary>
public sealed class ReadingProgress
{
    public Guid UserId { get; private set; }
    public Guid CanonicalBookId { get; private set; }
    public Guid CanonicalChapterId { get; private set; }
    public int ParagraphIndex { get; private set; }
    public int ProgressPercent { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ReadingProgress() { }

    public static ReadingProgress Create(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        int paragraphIndex,
        int progressPercent,
        DateTimeOffset now)
    {
        ReadingStateValidation.ValidateIdentity(userId, canonicalBookId);
        ReadingStateValidation.ValidateChapter(canonicalChapterId);
        ReadingStateValidation.ValidateProgress(paragraphIndex, progressPercent);

        return new ReadingProgress
        {
            UserId = userId,
            CanonicalBookId = canonicalBookId,
            CanonicalChapterId = canonicalChapterId,
            ParagraphIndex = paragraphIndex,
            ProgressPercent = progressPercent,
            UpdatedAt = now,
        };
    }

    public static ReadingProgress Rehydrate(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        int paragraphIndex,
        int progressPercent,
        DateTimeOffset updatedAt) =>
        new()
        {
            UserId = userId,
            CanonicalBookId = canonicalBookId,
            CanonicalChapterId = canonicalChapterId,
            ParagraphIndex = paragraphIndex,
            ProgressPercent = progressPercent,
            UpdatedAt = updatedAt,
        };

    public void Update(
        Guid canonicalChapterId,
        int paragraphIndex,
        int progressPercent,
        DateTimeOffset now)
    {
        ReadingStateValidation.ValidateChapter(canonicalChapterId);
        ReadingStateValidation.ValidateProgress(paragraphIndex, progressPercent);
        CanonicalChapterId = canonicalChapterId;
        ParagraphIndex = paragraphIndex;
        ProgressPercent = progressPercent;
        UpdatedAt = now;
    }
}

/// <summary>
/// 最近阅读历史按用户/书籍/章节去重，重复打开只更新最近访问时间。
/// </summary>
public sealed class ReadingHistoryEntry
{
    public Guid UserId { get; private set; }
    public Guid CanonicalBookId { get; private set; }
    public Guid CanonicalChapterId { get; private set; }
    public DateTimeOffset FirstReadAt { get; private set; }
    public DateTimeOffset LastReadAt { get; private set; }

    private ReadingHistoryEntry() { }

    public static ReadingHistoryEntry Create(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        DateTimeOffset now)
    {
        ReadingStateValidation.ValidateIdentity(userId, canonicalBookId);
        ReadingStateValidation.ValidateChapter(canonicalChapterId);

        return new ReadingHistoryEntry
        {
            UserId = userId,
            CanonicalBookId = canonicalBookId,
            CanonicalChapterId = canonicalChapterId,
            FirstReadAt = now,
            LastReadAt = now,
        };
    }

    public static ReadingHistoryEntry Rehydrate(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        DateTimeOffset firstReadAt,
        DateTimeOffset lastReadAt) =>
        new()
        {
            UserId = userId,
            CanonicalBookId = canonicalBookId,
            CanonicalChapterId = canonicalChapterId,
            FirstReadAt = firstReadAt,
            LastReadAt = lastReadAt,
        };

    public void Touch(DateTimeOffset now) => LastReadAt = now;
}

/// <summary>用户级阅读偏好，默认值在领域层集中定义并可增量演进。</summary>
public sealed class ReaderPreference
{
    public const int DefaultFontSizePercent = 100;
    public const int MinFontSizePercent = 80;
    public const int MaxFontSizePercent = 180;
    public const int DefaultLineHeightPercent = 180;
    public const int MinLineHeightPercent = 130;
    public const int MaxLineHeightPercent = 240;

    public Guid UserId { get; private set; }
    public int FontSizePercent { get; private set; }
    public int LineHeightPercent { get; private set; }
    public ReaderTheme Theme { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ReaderPreference() { }

    public static ReaderPreference CreateDefault(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("user id must not be empty.", nameof(userId));
        }

        return new ReaderPreference
        {
            UserId = userId,
            FontSizePercent = DefaultFontSizePercent,
            LineHeightPercent = DefaultLineHeightPercent,
            Theme = ReaderTheme.System,
            UpdatedAt = now,
        };
    }

    public static ReaderPreference Rehydrate(
        Guid userId,
        int fontSizePercent,
        int lineHeightPercent,
        ReaderTheme theme,
        DateTimeOffset updatedAt) =>
        new()
        {
            UserId = userId,
            FontSizePercent = fontSizePercent,
            LineHeightPercent = lineHeightPercent,
            Theme = theme,
            UpdatedAt = updatedAt,
        };

    public void Update(
        int fontSizePercent,
        int lineHeightPercent,
        ReaderTheme theme,
        DateTimeOffset now)
    {
        ReadingStateValidation.ValidatePreference(
            fontSizePercent, lineHeightPercent, theme);
        FontSizePercent = fontSizePercent;
        LineHeightPercent = lineHeightPercent;
        Theme = theme;
        UpdatedAt = now;
    }
}

internal static class ReadingStateValidation
{
    public static void ValidateIdentity(Guid userId, Guid canonicalBookId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("user id must not be empty.", nameof(userId));
        }

        if (canonicalBookId == Guid.Empty)
        {
            throw new ArgumentException("canonical book id must not be empty.", nameof(canonicalBookId));
        }
    }

    public static void ValidateChapter(Guid canonicalChapterId)
    {
        if (canonicalChapterId == Guid.Empty)
        {
            throw new ArgumentException(
                "canonical chapter id must not be empty.",
                nameof(canonicalChapterId));
        }
    }

    public static void ValidateShelfStatus(ShelfStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    public static void ValidateProgress(int paragraphIndex, int progressPercent)
    {
        if (paragraphIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paragraphIndex), "paragraph index must not be negative.");
        }

        if (progressPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progressPercent), "progress percent must be between 0 and 100.");
        }
    }

    public static void ValidatePreference(
        int fontSizePercent,
        int lineHeightPercent,
        ReaderTheme theme)
    {
        if (fontSizePercent is < ReaderPreference.MinFontSizePercent or > ReaderPreference.MaxFontSizePercent)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSizePercent));
        }

        if (lineHeightPercent is < ReaderPreference.MinLineHeightPercent or > ReaderPreference.MaxLineHeightPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(lineHeightPercent));
        }

        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme));
        }
    }
}
