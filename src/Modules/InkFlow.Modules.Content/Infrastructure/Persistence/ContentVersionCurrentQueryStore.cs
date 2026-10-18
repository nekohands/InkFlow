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
        var entity = await CurrentVersionQuery()
            .SingleOrDefaultAsync(
                v => v.CanonicalChapterId == canonicalChapterId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ContentDbContext.ToDomain(entity);
    }

    public async Task<IReadOnlyList<ContentVersion>> ListCurrentForBookAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var entities = await CurrentVersionQuery()
            .AsNoTracking()
            .Where(v => v.CanonicalBookId == canonicalBookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ContentDbContext.ToDomain).ToList();
    }

    public Task<Guid?> GetCurrentCanonicalBookIdAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default) =>
        CurrentVersionQuery()
            .Where(v => v.CanonicalChapterId == canonicalChapterId)
            .Select(v => (Guid?)v.CanonicalBookId)
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<ContentVersionEntity> CurrentVersionQuery() =>
        db.Versions.Where(version => version.IsCurrent);
}
