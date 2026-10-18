using InkFlow.Modules.Reading.Application;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Infrastructure.Persistence;

public static class ReadingMapper
{
    public static BookshelfEntryEntity ToEntity(BookshelfEntry entry) => new()
    {
        UserId = entry.UserId,
        CanonicalBookId = entry.CanonicalBookId,
        Status = (int)entry.Status,
        AddedAt = entry.AddedAt,
        UpdatedAt = entry.UpdatedAt,
    };

    public static BookshelfEntry ToDomain(BookshelfEntryEntity entity) =>
        BookshelfEntry.Rehydrate(
            entity.UserId,
            entity.CanonicalBookId,
            (ShelfStatus)entity.Status,
            entity.AddedAt,
            entity.UpdatedAt);

    public static ReadingProgressEntity ToEntity(ReadingProgress progress) => new()
    {
        UserId = progress.UserId,
        CanonicalBookId = progress.CanonicalBookId,
        CanonicalChapterId = progress.CanonicalChapterId,
        ParagraphIndex = progress.ParagraphIndex,
        ProgressPercent = progress.ProgressPercent,
        UpdatedAt = progress.UpdatedAt,
    };

    public static ReadingProgress ToDomain(ReadingProgressEntity entity) =>
        ReadingProgress.Rehydrate(
            entity.UserId,
            entity.CanonicalBookId,
            entity.CanonicalChapterId,
            entity.ParagraphIndex,
            entity.ProgressPercent,
            entity.UpdatedAt);

    public static ReadingHistoryEntryEntity ToEntity(ReadingHistoryEntry history) => new()
    {
        UserId = history.UserId,
        CanonicalBookId = history.CanonicalBookId,
        CanonicalChapterId = history.CanonicalChapterId,
        FirstReadAt = history.FirstReadAt,
        LastReadAt = history.LastReadAt,
    };

    public static ReadingHistoryEntry ToDomain(ReadingHistoryEntryEntity entity) =>
        ReadingHistoryEntry.Rehydrate(
            entity.UserId,
            entity.CanonicalBookId,
            entity.CanonicalChapterId,
            entity.FirstReadAt,
            entity.LastReadAt);

    public static ReaderPreferenceEntity ToEntity(ReaderPreference preference) => new()
    {
        UserId = preference.UserId,
        FontSizePercent = preference.FontSizePercent,
        LineHeightPercent = preference.LineHeightPercent,
        Theme = (int)preference.Theme,
        UpdatedAt = preference.UpdatedAt,
    };

    public static ReaderPreference ToDomain(ReaderPreferenceEntity entity) =>
        ReaderPreference.Rehydrate(
            entity.UserId,
            entity.FontSizePercent,
            entity.LineHeightPercent,
            (ReaderTheme)entity.Theme,
            entity.UpdatedAt);
}

public sealed class EfReadingStateRepository(ReadingDbContext db) : IReadingStateRepository
{
    private readonly ReadingShelfStore shelfStore = new(db);
    private readonly ReadingProgressHistoryStore progressHistoryStore = new(db);
    private readonly ReaderPreferenceStore preferenceStore = new(db);

    public Task<BookshelfEntry?> GetShelfEntryAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
        => shelfStore.GetShelfEntryAsync(userId, canonicalBookId, cancellationToken);

    public Task<IReadOnlyList<BookshelfEntry>> ListShelfAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        shelfStore.ListShelfAsync(userId, limit, cancellationToken);

    public Task UpsertShelfAsync(
        BookshelfEntry entry,
        CancellationToken cancellationToken = default)
        => shelfStore.UpsertShelfAsync(entry, cancellationToken);

    public Task RemoveShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default) =>
        shelfStore.RemoveShelfAsync(userId, canonicalBookId, cancellationToken);

    public Task<ReadingProgress?> GetProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
        => progressHistoryStore.GetProgressAsync(userId, canonicalBookId, cancellationToken);

    public Task<ReadingHistoryEntry?> GetHistoryAsync(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default)
        => progressHistoryStore.GetHistoryAsync(
            userId, canonicalBookId, canonicalChapterId, cancellationToken);

    public Task SaveProgressAsync(
        ReadingProgress progress,
        ReadingHistoryEntry history,
        CancellationToken cancellationToken = default)
        => progressHistoryStore.SaveProgressAsync(progress, history, cancellationToken);

    public Task<IReadOnlyList<ReadingHistoryEntry>> ListHistoryAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        progressHistoryStore.ListHistoryAsync(userId, limit, cancellationToken);

    public Task<ReaderPreference?> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => preferenceStore.GetPreferencesAsync(userId, cancellationToken);

    public Task UpsertPreferencesAsync(
        ReaderPreference preference,
        CancellationToken cancellationToken = default)
        => preferenceStore.UpsertPreferencesAsync(preference, cancellationToken);
}
