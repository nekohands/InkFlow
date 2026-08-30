using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>书籍包任务仓储；领取使用数据库行锁，避免多个 Worker 生成同一任务。</summary>
public sealed class EfBookPackageJobRepository(ContentDbContext db) : IBookPackageJobRepository
{
    public async Task AddAsync(BookPackageJob job, CancellationToken cancellationToken = default)
    {
        db.PackageJobs.Add(BookPackageJobMapper.ToEntity(job));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<BookPackageJob?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PackageJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : BookPackageJobMapper.ToDomain(entity);
    }

    public async Task<BookPackageJob?> TryLeaseAsync(
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidates = await db.PackageJobs
            .FromSqlInterpolated($"""
                SELECT *
                FROM "content"."package_jobs"
                WHERE (("Status" = {(int)BookPackageJobStatus.Queued}
                        AND ("ScheduledAt" IS NULL OR "ScheduledAt" <= {now}))
                   OR ("Status" = {(int)BookPackageJobStatus.Running}
                       AND "LeaseExpiresAt" IS NOT NULL
                       AND "LeaseExpiresAt" <= {now}))
                ORDER BY "CreatedAt", "Id"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = candidates.SingleOrDefault();
        if (entity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var job = BookPackageJobMapper.ToDomain(entity);
        job.Lease(owner, now, leaseDuration);
        BookPackageJobMapper.ApplyDomain(job, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return job;
    }

    public async Task SaveAsync(BookPackageJob job, CancellationToken cancellationToken = default)
    {
        var entity = await db.PackageJobs
            .SingleOrDefaultAsync(candidate => candidate.Id == job.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"package job {job.Id} does not exist; use {nameof(AddAsync)} first.");

        BookPackageJobMapper.ApplyDomain(job, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BookPackageJob>> ListExpiredAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var entities = await db.PackageJobs
            .AsNoTracking()
            .Where(job => job.Status == (int)BookPackageJobStatus.Completed &&
                          job.ExpiresAt <= now)
            .OrderBy(job => job.ExpiresAt)
            .Take(safeLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(BookPackageJobMapper.ToDomain).ToList();
    }
}
