using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Owns the durable parent collection-run row lock used by task persistence paths.</summary>
internal static class CrawlerParentRunLock
{
    public static async Task<CollectionRunEntity?> LockAsync(
        CrawlingDbContext db,
        Guid runId,
        CancellationToken cancellationToken,
        bool asNoTracking = false)
    {
        var query = db.Runs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."runs"
                WHERE "Id" = {runId}
                FOR UPDATE
                """);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
