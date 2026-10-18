using InkFlow.Modules.Reading.Application;
using InkFlow.Modules.Reading.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Reading.Infrastructure.Persistence;

/// <summary>进度与历史的读取及原子持久化责任。</summary>
internal sealed class ReadingProgressHistoryStore(ReadingDbContext db)
{
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
}
