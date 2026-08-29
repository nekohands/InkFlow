using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Developers.Infrastructure.Persistence;

public static class DeveloperMapper
{
    public static DeveloperApplicationEntity ToEntity(DeveloperApplication application) => new()
    {
        Id = application.Id,
        UserId = application.UserId,
        Name = application.Name,
        Environment = (int)application.Environment,
        CreatedAt = application.CreatedAt,
        RevokedAt = application.RevokedAt,
    };

    public static DeveloperApplication ToDomain(DeveloperApplicationEntity entity) =>
        DeveloperApplication.Rehydrate(
            entity.Id,
            entity.UserId,
            entity.Name,
            (DeveloperEnvironment)entity.Environment,
            entity.CreatedAt,
            entity.RevokedAt);

    public static DeveloperApiKeyEntity ToEntity(DeveloperApiKey key) => new()
    {
        Id = key.Id,
        UserId = key.UserId,
        ApplicationId = key.ApplicationId,
        Name = key.Name,
        Prefix = key.Prefix,
        SecretHash = key.SecretHash,
        Scope = key.Scope,
        Environment = (int)key.Environment,
        CreatedAt = key.CreatedAt,
        ExpiresAt = key.ExpiresAt,
        LastUsedAt = key.LastUsedAt,
        RevokedAt = key.RevokedAt,
    };

    public static DeveloperApiKey ToDomain(DeveloperApiKeyEntity entity) =>
        DeveloperApiKey.Rehydrate(
            entity.Id,
            entity.UserId,
            entity.ApplicationId,
            entity.Name,
            entity.Prefix,
            entity.SecretHash,
            entity.Scope,
            (DeveloperEnvironment)entity.Environment,
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.LastUsedAt,
            entity.RevokedAt);
}

public sealed class EfDeveloperApplicationRepository(DeveloperDbContext db)
    : IDeveloperApplicationRepository
{
    public async Task AddAsync(
        DeveloperApplication application,
        CancellationToken cancellationToken = default)
    {
        db.Applications.Add(DeveloperMapper.ToEntity(application));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeveloperApplication>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.Applications
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(DeveloperMapper.ToDomain).ToList();
    }

    public async Task<DeveloperApplication?> GetAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == applicationId && x.UserId == userId,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : DeveloperMapper.ToDomain(entity);
    }

    public async Task<bool> RevokeAsync(
        Guid userId,
        Guid applicationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Applications
            .SingleOrDefaultAsync(
                x => x.Id == applicationId && x.UserId == userId,
                cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        entity.RevokedAt ??= now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

public sealed class EfDeveloperApiKeyRepository(DeveloperDbContext db)
    : IDeveloperApiKeyRepository
{
    public async Task AddAsync(
        DeveloperApiKey key,
        CancellationToken cancellationToken = default)
    {
        db.ApiKeys.Add(DeveloperMapper.ToEntity(key));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeveloperApiKey>> ListForApplicationAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.ApiKeys
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.ApplicationId == applicationId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(DeveloperMapper.ToDomain).ToList();
    }

    public async Task<DeveloperApiKey?> GetAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ApiKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == keyId && x.UserId == userId && x.ApplicationId == applicationId,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : DeveloperMapper.ToDomain(entity);
    }

    public async Task<DeveloperApiKey?> FindByHashAsync(
        string secretHash,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ApiKeys
            .AsNoTracking()
            .Join(
                db.Applications.AsNoTracking(),
                key => key.ApplicationId,
                application => application.Id,
                (key, application) => new { key, application })
            .SingleOrDefaultAsync(
                x => x.key.SecretHash == secretHash &&
                     x.key.UserId == x.application.UserId &&
                     x.application.RevokedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : DeveloperMapper.ToDomain(entity.key);
    }

    public async Task<bool> RevokeAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ApiKeys
            .SingleOrDefaultAsync(
                x => x.Id == keyId && x.UserId == userId && x.ApplicationId == applicationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        entity.RevokedAt ??= now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RotateAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        DeveloperApiKey replacement,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = await db.ApiKeys
            .FromSqlInterpolated($"SELECT * FROM \"developers\".\"api_keys\" WHERE \"Id\" = {keyId} AND \"UserId\" = {userId} AND \"ApplicationId\" = {applicationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (current is null || current.RevokedAt is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        current.RevokedAt ??= now;
        db.ApiKeys.Add(DeveloperMapper.ToEntity(replacement));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task MarkUsedAsync(
        Guid keyId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await db.ApiKeys
            .Where(x => x.Id == keyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    x => x.LastUsedAt,
                    current => current.LastUsedAt == null || current.LastUsedAt < now
                        ? now
                        : current.LastUsedAt),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
