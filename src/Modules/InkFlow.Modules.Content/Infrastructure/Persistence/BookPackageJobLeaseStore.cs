using InkFlow.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>Owns durable package-job leasing and stale-writer fencing.</summary>
internal sealed class BookPackageJobLeaseStore(ContentDbContext db)
{
    public async Task<BookPackageJob?> TryLeaseAsync(
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
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
            if (job.Status == BookPackageJobStatus.Running &&
                job.AttemptCount >= job.MaxAttempts)
            {
                job.FailExpiredLease(now);
                BookPackageJobMapper.ApplyDomain(job, entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            job.Lease(owner, now, leaseDuration);
            BookPackageJobMapper.ApplyDomain(job, entity);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return job;
        }
    }

    public async Task<bool> SaveLeasedAsync(
        BookPackageJob job,
        string leaseOwner,
        int leaseAttempt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (string.IsNullOrWhiteSpace(leaseOwner) || leaseAttempt < 1)
        {
            throw new ArgumentException("package lease identity is invalid.");
        }

        var affected = await db.PackageJobs
            .Where(candidate =>
                candidate.Id == job.Id &&
                candidate.Status == (int)BookPackageJobStatus.Running &&
                candidate.LeaseOwner == leaseOwner &&
                candidate.AttemptCount == leaseAttempt &&
                candidate.LeaseExpiresAt != null &&
                candidate.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Status, _ => (int)job.Status)
                    .SetProperty(candidate => candidate.AttemptCount, _ => job.AttemptCount)
                    .SetProperty(candidate => candidate.MaxAttempts, _ => job.MaxAttempts)
                    .SetProperty(candidate => candidate.ScheduledAt, _ => job.ScheduledAt)
                    .SetProperty(candidate => candidate.LeaseOwner, _ => job.LeaseOwner)
                    .SetProperty(candidate => candidate.LeaseExpiresAt, _ => job.LeaseExpiresAt)
                    .SetProperty(candidate => candidate.TotalChapterCount, _ => job.TotalChapterCount)
                    .SetProperty(candidate => candidate.CompletedChapterCount, _ => job.CompletedChapterCount)
                    .SetProperty(candidate => candidate.ArtifactFileName, _ => job.ArtifactFileName)
                    .SetProperty(candidate => candidate.ArtifactSha256, _ => job.ArtifactSha256)
                    .SetProperty(candidate => candidate.ArtifactLength, _ => job.ArtifactLength)
                    .SetProperty(candidate => candidate.FailureReason, _ => job.FailureReason)
                    .SetProperty(candidate => candidate.UpdatedAt, _ => job.UpdatedAt)
                    .SetProperty(candidate => candidate.ExpiresAt, _ => job.ExpiresAt),
                cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }
}
