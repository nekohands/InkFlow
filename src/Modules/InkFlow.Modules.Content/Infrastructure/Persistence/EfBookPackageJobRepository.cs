using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>书籍包任务仓储；领取使用数据库行锁，避免多个 Worker 生成同一任务。</summary>
public sealed class EfBookPackageJobRepository(ContentDbContext db) : IBookPackageJobRepository
{
    private readonly BookPackageJobLeaseStore leaseStore = new(db);

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

    public async Task<IReadOnlyList<BookPackageJob>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var entities = await db.PackageJobs
            .AsNoTracking()
            .OrderByDescending(job => job.UpdatedAt)
            .ThenByDescending(job => job.Id)
            .Take(safeLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(BookPackageJobMapper.ToDomain).ToList();
    }

    public Task<BookPackageJob?> TryLeaseAsync(
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) =>
        leaseStore.TryLeaseAsync(now, owner, leaseDuration, cancellationToken);

    public async Task SaveAsync(BookPackageJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Status == BookPackageJobStatus.Running)
        {
            throw new InvalidOperationException(
                "running package jobs must be saved through the fenced lease operation.");
        }

        var entity = await db.PackageJobs
            .SingleOrDefaultAsync(candidate => candidate.Id == job.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"package job {job.Id} does not exist; use {nameof(AddAsync)} first.");

        BookPackageJobMapper.ApplyDomain(job, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> SaveLeasedAsync(
        BookPackageJob job,
        string leaseOwner,
        int leaseAttempt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        leaseStore.SaveLeasedAsync(job, leaseOwner, leaseAttempt, now, cancellationToken);

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
