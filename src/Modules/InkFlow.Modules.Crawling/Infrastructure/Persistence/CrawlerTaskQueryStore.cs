using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Owns ordinary crawler-task queries and leasable candidate discovery.</summary>
internal sealed class CrawlerTaskQueryStore(CrawlingDbContext db)
{
    public async Task<CrawlerTask?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Tasks.FindAsync([id], cancellationToken).ConfigureAwait(false);
        return entity is null ? null : CrawlerTaskMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.Tasks
            .Where(t =>
                (t.RunId == null || db.Runs.Any(r =>
                    r.Id == t.RunId &&
                    (r.Status == (int)CollectionRunStatus.Pending ||
                     r.Status == (int)CollectionRunStatus.Running))) &&
                ((t.Status == (int)CrawlerTaskStatus.Pending &&
                 (t.ScheduledAt == null || t.ScheduledAt <= now)) ||
                ((t.Status == (int)CrawlerTaskStatus.Leased ||
                  t.Status == (int)CrawlerTaskStatus.Running) &&
                 t.LeaseExpiresAt != null && t.LeaseExpiresAt <= now)))
            .OrderBy(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(CrawlerTaskMapper.ToDomain).ToList();
    }
}
