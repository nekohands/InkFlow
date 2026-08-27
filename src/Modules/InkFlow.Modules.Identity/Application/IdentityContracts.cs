using System.Globalization;
using InkFlow.Modules.Identity.Domain;
using Microsoft.Extensions.Configuration;

namespace InkFlow.Modules.Identity.Application;

public sealed class IdentityOptions
{
    public const string ConfigurationSectionName = "Identity";

    public int AccessTokenLifetimeMinutes { get; init; } = 15;
    public int RefreshTokenLifetimeDays { get; init; } = 30;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenLifetimeMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenLifetimeDays);

    public static IdentityOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new IdentityOptions
        {
            AccessTokenLifetimeMinutes = ReadInt(
                section, nameof(AccessTokenLifetimeMinutes), 15),
            RefreshTokenLifetimeDays = ReadInt(
                section, nameof(RefreshTokenLifetimeDays), 30),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (AccessTokenLifetimeMinutes is < 5 or > 60)
        {
            throw new InvalidOperationException(
                "Identity:AccessTokenLifetimeMinutes must be between 5 and 60.");
        }

        if (RefreshTokenLifetimeDays is < 1 or > 365)
        {
            throw new InvalidOperationException(
                "Identity:RefreshTokenLifetimeDays must be between 1 and 365.");
        }
    }

    private static int ReadInt(IConfiguration section, string key, int defaultValue)
    {
        var raw = section[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"Identity:{key} must be an integer.");
        }

        return value;
    }
}

public enum IdentityResultStatus
{
    Success,
    InvalidRequest,
    EmailAlreadyRegistered,
    InvalidCredentials,
    InvalidRefreshToken,
}

public static class IdentityPolicies
{
    public const string CrawlerRepair = "identity-crawler-repair";
    public const string ContentModeration = "identity-content-moderation";
}

public sealed record AuthenticatedIdentity(
    Guid UserId,
    string Email,
    UserRole Role,
    Guid SessionId);

public sealed record AuthSession(
    Guid SessionId,
    Guid UserId,
    string Email,
    UserRole Role,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record IdentityOperationResult(
    IdentityResultStatus Status,
    AuthSession? Session = null)
{
    public bool IsSuccess => Status == IdentityResultStatus.Success && Session is not null;

    public static IdentityOperationResult Success(AuthSession session) =>
        new(IdentityResultStatus.Success, session);

    public static IdentityOperationResult Failure(IdentityResultStatus status) =>
        new(status);
}

public interface IUserRepository
{
    Task<User?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveAsync(User user, CancellationToken cancellationToken = default);
}

public interface IIdentitySessionRepository
{
    Task<RefreshSession?> FindRefreshSessionAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default);

    Task<AccessToken?> FindAccessTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task AddSessionAsync(
        RefreshSession session,
        AccessToken accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在存储层以行锁完成 refresh token 的一次性轮换；并发请求至多一个成功。
    /// </summary>
    Task<bool> RotateRefreshSessionAsync(
        string currentRefreshTokenHash,
        RefreshSession replacement,
        AccessToken replacementAccessToken,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IOpaqueTokenGenerator
{
    string CreateToken();
}

public interface IIdentityService
{
    Task<IdentityOperationResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedIdentity?> ValidateAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
