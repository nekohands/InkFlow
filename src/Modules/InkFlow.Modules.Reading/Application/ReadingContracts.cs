using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

public sealed record ReadingShelfItem(
    Guid BookId,
    string Title,
    string Author,
    int ChapterCount,
    string Status,
    DateTimeOffset AddedAt,
    DateTimeOffset UpdatedAt,
    Guid? CurrentChapterId,
    string? CurrentChapterTitle,
    int? ProgressPercent,
    DateTimeOffset? LastReadAt);

public sealed record ReadingHistoryItem(
    Guid BookId,
    string Title,
    string Author,
    Guid ChapterId,
    string ChapterTitle,
    int ChapterIndex,
    DateTimeOffset FirstReadAt,
    DateTimeOffset LastReadAt);

public sealed record ReadingProgressView(
    Guid BookId,
    Guid ChapterId,
    string ChapterTitle,
    int ChapterIndex,
    int ParagraphIndex,
    int ProgressPercent,
    DateTimeOffset UpdatedAt);

public sealed record ReaderPreferenceView(
    int FontSizePercent,
    int LineHeightPercent,
    string Theme,
    DateTimeOffset UpdatedAt);

public enum ReadingResultStatus
{
    Success,
    InvalidRequest,
    NotFound,
}

public sealed record ReadingOperationResult<T>(
    ReadingResultStatus Status,
    T? Value)
{
    public bool IsSuccess => Status == ReadingResultStatus.Success && Value is not null;

    public static ReadingOperationResult<T> Ok(T value) =>
        new(ReadingResultStatus.Success, value);

    public static ReadingOperationResult<T> Failure(ReadingResultStatus status) =>
        new(status, default);
}

/// <summary>
/// Reading 的持久化端口。每个查询和写入都显式携带 UserId，避免形成无范围的私人数据访问。
/// </summary>
public interface IReadingStateRepository
{
    Task<BookshelfEntry?> GetShelfEntryAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookshelfEntry>> ListShelfAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task UpsertShelfAsync(
        BookshelfEntry entry,
        CancellationToken cancellationToken = default);

    Task RemoveShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);

    Task<ReadingProgress?> GetProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);

    Task<ReadingHistoryEntry?> GetHistoryAsync(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default);

    /// <summary>在一次持久化边界内保存当前进度和最近阅读记录。</summary>
    Task SaveProgressAsync(
        ReadingProgress progress,
        ReadingHistoryEntry history,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReadingHistoryEntry>> ListHistoryAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ReaderPreference?> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpsertPreferencesAsync(
        ReaderPreference preference,
        CancellationToken cancellationToken = default);
}

public interface IReadingStateService
{
    Task<IReadOnlyList<ReadingShelfItem>> ListShelfAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ReadingOperationResult<ReadingShelfItem>> PutShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        ShelfStatus status,
        CancellationToken cancellationToken = default);

    Task<ReadingResultStatus> RemoveShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReadingHistoryItem>> ListHistoryAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ReadingProgressView?> GetProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);

    Task<ReadingOperationResult<ReadingProgressView>> SaveProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        int paragraphIndex,
        int progressPercent,
        CancellationToken cancellationToken = default);

    Task<ReaderPreferenceView> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ReadingOperationResult<ReaderPreferenceView>> UpdatePreferencesAsync(
        Guid userId,
        int? fontSizePercent,
        int? lineHeightPercent,
        ReaderTheme? theme,
        CancellationToken cancellationToken = default);
}
