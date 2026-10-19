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
        var entity = await ChapterVersionQuery(canonicalChapterId)
            .SingleOrDefaultAsync(v => v.CanonicalHash == canonicalHash, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ContentDbContext.ToDomain(entity);
    }

    public async Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default)
    {
        var entities = await ChapterVersionQuery(canonicalChapterId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ContentDbContext.ToDomain).ToList();
    }

    private IQueryable<ContentVersionEntity> ChapterVersionQuery(Guid canonicalChapterId) =>
        db.Versions.Where(version => version.CanonicalChapterId == canonicalChapterId);
}
