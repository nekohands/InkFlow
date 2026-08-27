using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Identity.Infrastructure.Persistence;

public static class IdentityMapper
{
    public static UserEntity ToEntity(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        NormalizedEmail = user.NormalizedEmail,
        PasswordHash = user.PasswordHash,
        Role = (int)user.Role,
        Status = (int)user.Status,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
    };

    public static User ToDomain(UserEntity entity) =>
        User.Rehydrate(
            entity.Id,
            entity.Email,
            entity.NormalizedEmail,
            entity.PasswordHash,
            (UserRole)entity.Role,
            (UserStatus)entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt);

    public static RefreshSessionEntity ToEntity(RefreshSession session) => new()
    {
        Id = session.Id,
        UserId = session.UserId,
        RefreshTokenHash = session.RefreshTokenHash,
        CreatedAt = session.CreatedAt,
        ExpiresAt = session.ExpiresAt,
        RevokedAt = session.RevokedAt,
        ReplacedBySessionId = session.ReplacedBySessionId,
    };

    public static RefreshSession ToDomain(RefreshSessionEntity entity) =>
        RefreshSession.Rehydrate(
            entity.Id,
            entity.UserId,
            entity.RefreshTokenHash,
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.RevokedAt,
            entity.ReplacedBySessionId);

    public static AccessTokenEntity ToEntity(AccessToken token) => new()
    {
        Id = token.Id,
        UserId = token.UserId,
        SessionId = token.SessionId,
        TokenHash = token.TokenHash,
        CreatedAt = token.CreatedAt,
        ExpiresAt = token.ExpiresAt,
        RevokedAt = token.RevokedAt,
    };

    public static AccessToken ToDomain(AccessTokenEntity entity) =>
        AccessToken.Rehydrate(
            entity.Id,
            entity.UserId,
            entity.SessionId,
            entity.TokenHash,
            entity.CreatedAt,
            entity.ExpiresAt,
            entity.RevokedAt);
}

public sealed class EfUserRepository(IdentityDbContext db) : IUserRepository
{
    public async Task<User?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : IdentityMapper.ToDomain(entity);
    }

    public async Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : IdentityMapper.ToDomain(entity);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(IdentityMapper.ToEntity(user));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(User user, CancellationToken cancellationToken = default)
    {
        var entity = await db.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == user.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"user {user.Id} does not exist.");

        entity.Email = user.Email;
        entity.NormalizedEmail = user.NormalizedEmail;
        entity.PasswordHash = user.PasswordHash;
        entity.Role = (int)user.Role;
        entity.Status = (int)user.Status;
        entity.UpdatedAt = user.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class EfIdentitySessionRepository(IdentityDbContext db) : IIdentitySessionRepository
{
    public async Task<RefreshSession?> FindRefreshSessionAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                session => session.RefreshTokenHash == refreshTokenHash,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : IdentityMapper.ToDomain(entity);
    }

    public async Task<AccessToken?> FindAccessTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.AccessTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : IdentityMapper.ToDomain(entity);
    }

    public async Task AddSessionAsync(
        RefreshSession session,
        AccessToken accessToken,
        CancellationToken cancellationToken = default)
    {
        if (accessToken.SessionId != session.Id || accessToken.UserId != session.UserId)
        {
            throw new InvalidOperationException("access token must belong to the new refresh session.");
        }

        db.Sessions.Add(IdentityMapper.ToEntity(session));
        db.AccessTokens.Add(IdentityMapper.ToEntity(accessToken));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RotateRefreshSessionAsync(
        string currentRefreshTokenHash,
        RefreshSession replacement,
        AccessToken replacementAccessToken,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (replacementAccessToken.SessionId != replacement.Id ||
            replacementAccessToken.UserId != replacement.UserId)
        {
            throw new InvalidOperationException("replacement access token does not match the replacement session.");
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var current = await db.Sessions
            .FromSqlInterpolated($"SELECT * FROM \"identity\".\"sessions\" WHERE \"RefreshTokenHash\" = {currentRefreshTokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (current is null || current.RevokedAt is not null || current.ExpiresAt <= now)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        current.RevokedAt ??= now;
        current.ReplacedBySessionId ??= replacement.Id;
        db.Sessions.Add(IdentityMapper.ToEntity(replacement));
        db.AccessTokens.Add(IdentityMapper.ToEntity(replacementAccessToken));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var session = await db.Sessions
            .SingleOrDefaultAsync(candidate => candidate.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return;
        }

        session.RevokedAt ??= now;
        var accessTokens = await db.AccessTokens
            .Where(token => token.SessionId == sessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var accessToken in accessTokens)
        {
            accessToken.RevokedAt ??= now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
