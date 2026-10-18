using InkFlow.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// 内容版本历史与去重查询边界。
/// </summary>
internal sealed class ContentVersionHistoryQueryStore(ContentDbContext db)
{
    public async Task<ContentVersion?> FindByHashAsync(
        Guid canonicalChapterId,
        string canonicalHash,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Versions
            .SingleOrDefaultAsync(
                v => v.CanonicalChapterId == canonicalChapterId && v.CanonicalHash == canonicalHash,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ContentDbContext.ToDomain(entity);
    }

    public async Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.Versions
            .Where(v => v.CanonicalChapterId == canonicalChapterId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ContentDbContext.ToDomain).ToList();
    }
}
