using InkFlow.Modules.Reading.Application;
using InkFlow.Modules.Reading.Domain;
using Microsoft.EntityFrameworkCore;

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
    public async Task<BookshelfEntry?> GetShelfEntryAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ShelfEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.UserId == userId && entry.CanonicalBookId == canonicalBookId,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ReadingMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<BookshelfEntry>> ListShelfAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        (await db.ShelfEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.UpdatedAt)
            .ThenByDescending(entry => entry.CanonicalBookId)
            .Take(Math.Clamp(limit, 1, ReadingStateService.MaxPageSize))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
        .Select(ReadingMapper.ToDomain)
        .ToList();

    public async Task UpsertShelfAsync(
        BookshelfEntry entry,
        CancellationToken cancellationToken = default)
    {
        var entity = ReadingMapper.ToEntity(entry);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "reading"."shelf_entries" AS target ("UserId", "CanonicalBookId", "Status", "AddedAt", "UpdatedAt")
            VALUES ({entity.UserId}, {entity.CanonicalBookId}, {entity.Status}, {entity.AddedAt}, {entity.UpdatedAt})
            ON CONFLICT ("UserId", "CanonicalBookId") DO UPDATE
            SET "Status" = EXCLUDED."Status",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            WHERE target."UpdatedAt" <= EXCLUDED."UpdatedAt";
            """, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default) =>
        await db.ShelfEntries
            .Where(entry => entry.UserId == userId && entry.CanonicalBookId == canonicalBookId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<ReadingProgress?> GetProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Progress
            .AsNoTracking()
            .SingleOrDefaultAsync(
                progress => progress.UserId == userId && progress.CanonicalBookId == canonicalBookId,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ReadingMapper.ToDomain(entity);
    }

    public async Task<ReadingHistoryEntry?> GetHistoryAsync(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.History
            .AsNoTracking()
            .SingleOrDefaultAsync(
                history => history.UserId == userId &&
                           history.CanonicalBookId == canonicalBookId &&
                           history.CanonicalChapterId == canonicalChapterId,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ReadingMapper.ToDomain(entity);
    }

    public async Task SaveProgressAsync(
        ReadingProgress progress,
        ReadingHistoryEntry history,
        CancellationToken cancellationToken = default)
    {
        if (progress.UserId != history.UserId ||
            progress.CanonicalBookId != history.CanonicalBookId)
        {
            throw new InvalidOperationException(
                "progress and history must belong to the same user and canonical book.");
        }

        var progressEntity = ReadingMapper.ToEntity(progress);
        var historyEntity = ReadingMapper.ToEntity(history);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "reading"."progress" AS target ("UserId", "CanonicalBookId", "CanonicalChapterId", "ParagraphIndex", "ProgressPercent", "UpdatedAt")
            VALUES ({progressEntity.UserId}, {progressEntity.CanonicalBookId}, {progressEntity.CanonicalChapterId}, {progressEntity.ParagraphIndex}, {progressEntity.ProgressPercent}, {progressEntity.UpdatedAt})
            ON CONFLICT ("UserId", "CanonicalBookId") DO UPDATE
            SET "CanonicalChapterId" = EXCLUDED."CanonicalChapterId",
                "ParagraphIndex" = EXCLUDED."ParagraphIndex",
                "ProgressPercent" = EXCLUDED."ProgressPercent",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            WHERE target."UpdatedAt" <= EXCLUDED."UpdatedAt";
            """, cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "reading"."history" AS target ("UserId", "CanonicalBookId", "CanonicalChapterId", "FirstReadAt", "LastReadAt")
            VALUES ({historyEntity.UserId}, {historyEntity.CanonicalBookId}, {historyEntity.CanonicalChapterId}, {historyEntity.FirstReadAt}, {historyEntity.LastReadAt})
            ON CONFLICT ("UserId", "CanonicalBookId", "CanonicalChapterId") DO UPDATE
            SET "LastReadAt" = EXCLUDED."LastReadAt"
            WHERE target."LastReadAt" <= EXCLUDED."LastReadAt";
            """, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReadingHistoryEntry>> ListHistoryAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        (await db.History
            .AsNoTracking()
            .Where(history => history.UserId == userId)
            .OrderByDescending(history => history.LastReadAt)
            .ThenByDescending(history => history.CanonicalBookId)
            .Take(Math.Clamp(limit, 1, ReadingStateService.MaxPageSize))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
        .Select(ReadingMapper.ToDomain)
        .ToList();

    public async Task<ReaderPreference?> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Preferences
            .AsNoTracking()
            .SingleOrDefaultAsync(preference => preference.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ReadingMapper.ToDomain(entity);
    }

    public async Task UpsertPreferencesAsync(
        ReaderPreference preference,
        CancellationToken cancellationToken = default)
    {
        var entity = ReadingMapper.ToEntity(preference);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "reading"."preferences" AS target ("UserId", "FontSizePercent", "LineHeightPercent", "Theme", "UpdatedAt")
            VALUES ({entity.UserId}, {entity.FontSizePercent}, {entity.LineHeightPercent}, {entity.Theme}, {entity.UpdatedAt})
            ON CONFLICT ("UserId") DO UPDATE
            SET "FontSizePercent" = EXCLUDED."FontSizePercent",
                "LineHeightPercent" = EXCLUDED."LineHeightPercent",
                "Theme" = EXCLUDED."Theme",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            WHERE target."UpdatedAt" <= EXCLUDED."UpdatedAt";
            """, cancellationToken).ConfigureAwait(false);
    }
}
