using InkFlow.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// 内容策略最新决策的只读查询边界。
/// </summary>
internal sealed class ContentPolicyQueryStore(ContentDbContext db)
{
    public async Task<ContentPolicyDecision?> GetLatestAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PolicyDecisions
            .AsNoTracking()
            .Where(x => x.CanonicalBookId == canonicalBookId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<ContentPolicyDecision>> ListLatestForBooksAsync(
        IReadOnlyCollection<Guid> canonicalBookIds,
        CancellationToken cancellationToken = default)
    {
        var safeIds = canonicalBookIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (safeIds.Length == 0)
        {
            return [];
        }

        var entities = await LatestPerBookQuery()
            .Where(x => safeIds.Contains(x.CanonicalBookId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<ContentPolicyDecision>> ListLatestAsync(
        bool takenDownOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = LatestPerBookQuery();

        if (takenDownOnly)
        {
            query = query.Where(decision => decision.Action == ContentPolicyAction.Takedown);
        }

        var entities = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
    }

    private IQueryable<ContentPolicyDecisionEntity> LatestPerBookQuery() =>
        db.PolicyDecisions
            .AsNoTracking()
            .Where(decision => !db.PolicyDecisions.Any(other =>
                other.CanonicalBookId == decision.CanonicalBookId &&
                (other.CreatedAt > decision.CreatedAt ||
                 (other.CreatedAt == decision.CreatedAt && other.Id > decision.Id))));

    private static ContentPolicyDecision ToDomain(ContentPolicyDecisionEntity entity) =>
        ContentPolicyDecision.Rehydrate(
            entity.Id,
            entity.CanonicalBookId,
            entity.Action,
            entity.ActorId,
            entity.Reason,
            entity.CreatedAt);
}
