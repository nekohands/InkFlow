using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

/// <summary>Owns ordinary source queries and stable source listing projections.</summary>
internal sealed class SourceQueryStore(SourcesDbContext db)
{
    public async Task<Source?> GetAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Sources.FindAsync([sourceId], cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<bool?> GetEnabledAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        return await db.Sources
            .AsNoTracking()
            .Where(source => source.Id == sourceId)
            .Select(source => (bool?)source.IsEnabled)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Source>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await db.Sources
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<SourcePage> ListPageAsync(
        SourceScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var query = db.Sources.AsNoTracking();
        if (after is not null)
        {
            query = query.Where(source => source.Id.CompareTo(after.Id) > 0);
        }

        var entries = await query
            .OrderBy(source => source.Id)
            .Take(limit + 1)
            .Select(source => new SourcePageEntry(
                source.Id,
                source.BaseUrl,
                source.IsEnabled))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasMore = entries.Count > limit;
        if (hasMore)
        {
            entries.RemoveAt(limit);
        }

        return new SourcePage(
            entries,
            hasMore ? new SourceScanCursor(entries[^1].Id) : null,
            hasMore);
    }

    private static Source ToDomain(SourceEntity entity) =>
        Source.Rehydrate(
            entity.Id,
            entity.DisplayName,
            entity.BaseUrl,
            SourcesDbContext.DeserializeRuleDsl(entity.RuleDslJson),
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.DefaultCredentialReferenceId,
            entity.IsEnabled);
}
