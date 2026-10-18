using InkFlow.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>Owns read-only book-package job queries and expiry selection.</summary>
internal sealed class BookPackageJobQueryStore(ContentDbContext db)
{
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
