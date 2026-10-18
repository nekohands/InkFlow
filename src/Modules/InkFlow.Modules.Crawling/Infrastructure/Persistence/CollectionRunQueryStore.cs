using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Owns read-only collection-run queries and stable cursor pagination.</summary>
internal sealed class CollectionRunQueryStore(CrawlingDbContext db)
{
    public async Task<CollectionRun?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Runs
            .AsNoTracking()
            .SingleOrDefaultAsync(run => run.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : CollectionRunMapper.ToDomain(entity);
    }

    public async Task<CollectionRun?> FindActiveAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            (int)CollectionRunStatus.Pending,
            (int)CollectionRunStatus.Running,
            (int)CollectionRunStatus.Paused,
            (int)CollectionRunStatus.Stopping,
        };

        var entity = await db.Runs
            .AsNoTracking()
            .Where(run => run.SourceId == sourceId &&
                          run.ExternalBookId == externalBookId &&
                          activeStatuses.Contains(run.Status))
            .OrderByDescending(run => run.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : CollectionRunMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<CollectionRun>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var page = await ListPageAsync(limit, null, cancellationToken).ConfigureAwait(false);
        return page.Entries;
    }

    public async Task<CollectionRunPage> ListPageAsync(
        int limit,
        CollectionRunCursor? before = null,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var query = db.Runs
            .AsNoTracking()
            .AsQueryable();
        if (before is not null)
        {
            query = query.Where(run =>
                run.UpdatedAt < before.UpdatedAt ||
                (run.UpdatedAt == before.UpdatedAt &&
                 run.Id.CompareTo(before.Id) < 0));
        }

        var entities = await query
            .OrderByDescending(run => run.UpdatedAt)
            .ThenByDescending(run => run.Id)
            .Take(safeLimit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasMore = entities.Count > safeLimit;
        var pageEntities = entities.Take(safeLimit).ToList();
        var entries = pageEntities.Select(CollectionRunMapper.ToDomain).ToList();
        var nextCursor = hasMore && pageEntities.Count > 0
            ? new CollectionRunCursor(
                pageEntities[^1].UpdatedAt,
                pageEntities[^1].Id)
            : null;

        return new(entries, nextCursor);
    }
}
