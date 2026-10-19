using InkFlow.Modules.Reading.Application;
using InkFlow.Modules.Reading.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Reading.Infrastructure.Persistence;

/// <summary>书架表的读写持久化责任。</summary>
internal sealed class ReadingShelfStore(ReadingDbContext db)
{
    public async Task<BookshelfEntry?> GetShelfEntryAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var entity = await ShelfEntryQuery(userId, canonicalBookId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
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
        await ShelfEntryQuery(userId, canonicalBookId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private IQueryable<BookshelfEntryEntity> ShelfEntryQuery(
        Guid userId,
        Guid canonicalBookId) =>
        db.ShelfEntries.Where(entry =>
            entry.UserId == userId && entry.CanonicalBookId == canonicalBookId);
}
