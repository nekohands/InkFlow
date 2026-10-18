using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

/// <summary>Owns read-only source capability health queries and stable unhealthy paging.</summary>
internal sealed class SourceHealthQueryStore(SourcesDbContext db)
{
    public async Task<SourceCapabilityHealth?> GetAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.CapabilityHealth
            .FindAsync([sourceId, capability], cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : SourceHealthMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.CapabilityHealth
            .Where(x => x.SourceId == sourceId)
            .OrderBy(x => x.Capability)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SourceHealthMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<SourceCapabilityHealth>> ListUnhealthyAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await db.CapabilityHealth
            .Where(x => x.Status == SourceHealthStatus.Unhealthy)
            .OrderBy(x => x.SourceId)
            .ThenBy(x => x.Capability)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(SourceHealthMapper.ToDomain).ToList();
    }

    public async Task<SourceHealthPage> ListUnhealthyPageAsync(
        SourceHealthScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var query = db.CapabilityHealth
            .AsNoTracking()
            .Where(x => x.Status == SourceHealthStatus.Unhealthy);
        if (after is not null)
        {
            var sourceId = after.SourceId;
            var capability = after.Capability;
            query = query.Where(x => x.SourceId.CompareTo(sourceId) > 0
                || (x.SourceId == sourceId && x.Capability > capability));
        }

        var entities = await query
            .OrderBy(x => x.SourceId)
            .ThenBy(x => x.Capability)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = entities.Count > limit;
        if (hasMore)
        {
            entities.RemoveAt(limit);
        }

        var health = entities.Select(SourceHealthMapper.ToDomain).ToList();
        var nextCursor = hasMore
            ? new SourceHealthScanCursor(health[^1].SourceId, health[^1].Capability)
            : null;
        return new SourceHealthPage(health, nextCursor, hasMore);
    }
}
