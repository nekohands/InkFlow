using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Projects crawler task statuses into a collection-run progress snapshot.</summary>
internal sealed class CollectionRunTaskProgressReader(CrawlingDbContext db)
{
    public async Task<CollectionRunTaskProgress> ReadAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var statuses = await db.Tasks
            .AsNoTracking()
            .Where(task => task.RunId == runId)
            .Select(task => task.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new(
            statuses.Count,
            statuses.Count(status => status == (int)CrawlerTaskStatus.Pending),
            statuses.Count(status => status == (int)CrawlerTaskStatus.Leased),
            statuses.Count(status => status == (int)CrawlerTaskStatus.Running),
            statuses.Count(status => status == (int)CrawlerTaskStatus.Completed),
            statuses.Count(status => status == (int)CrawlerTaskStatus.DeadLettered),
            statuses.Count(status => status == (int)CrawlerTaskStatus.Cancelled));
    }
}
