using InkFlow.Modules.Reading.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Reading.Infrastructure.Persistence;

/// <summary>阅读偏好表的读写持久化责任。</summary>
internal sealed class ReaderPreferenceStore(ReadingDbContext db)
{
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
