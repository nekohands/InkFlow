using InkFlow.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// 当前内容版本的读取投影边界。
/// </summary>
internal sealed class ContentVersionCurrentQueryStore(ContentDbContext db)
{
    public async Task<ContentVersion?> GetCurrentForChapterAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Versions
            .SingleOrDefaultAsync(
                v => v.CanonicalChapterId == canonicalChapterId && v.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ContentDbContext.ToDomain(entity);
    }

    public async Task<IReadOnlyList<ContentVersion>> ListCurrentForBookAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.Versions
            .AsNoTracking()
            .Where(v => v.CanonicalBookId == canonicalBookId && v.IsCurrent)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ContentDbContext.ToDomain).ToList();
    }

    public Task<Guid?> GetCurrentCanonicalBookIdAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default) =>
        db.Versions
            .Where(v => v.CanonicalChapterId == canonicalChapterId && v.IsCurrent)
            .Select(v => (Guid?)v.CanonicalBookId)
            .SingleOrDefaultAsync(cancellationToken);
}
