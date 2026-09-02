using System.Globalization;
using InkFlow.Modules.Identity.Domain;
using Microsoft.Extensions.Configuration;

namespace InkFlow.Modules.Identity.Application;

public sealed class IdentityOptions
{
    public const string ConfigurationSectionName = "Identity";

    public int AccessTokenLifetimeMinutes { get; init; } = 15;
    public int RefreshTokenLifetimeDays { get; init; } = 30;
    public int LegadoTokenLifetimeDays { get; init; } = 90;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenLifetimeMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenLifetimeDays);
    public TimeSpan LegadoTokenLifetime => TimeSpan.FromDays(LegadoTokenLifetimeDays);

    public static IdentityOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new IdentityOptions
        {
            AccessTokenLifetimeMinutes = ReadInt(
                section, nameof(AccessTokenLifetimeMinutes), 15),
            RefreshTokenLifetimeDays = ReadInt(
                section, nameof(RefreshTokenLifetimeDays), 30),
            LegadoTokenLifetimeDays = ReadInt(
                section, nameof(LegadoTokenLifetimeDays), 90),
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

        if (LegadoTokenLifetimeDays is < 1 or > 365)
        {
            throw new InvalidOperationException(
                "Identity:LegadoTokenLifetimeDays must be between 1 and 365.");
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
    public const string OperationsRead = "identity-operations-read";
    public const string AuditRead = "identity-audit-read";
    public const string CrawlerRepair = "identity-crawler-repair";
    public const string ContentModeration = "identity-content-moderation";
    public const string SourceOperations = "identity-source-operations";
    public const string CommercialManagement = "identity-commercial-management";
    public const string PermissionManagement = "identity-permission-management";
    public const string SourceCredentialManagement = "identity-source-credential-management";
    public const string LegadoRead = "identity-legado-read";
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

public sealed record IdentityProfile(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public enum ProfileResultStatus
{
    Success,
    InvalidRequest,
    NotFound,
}

public sealed record ProfileOperationResult(
    ProfileResultStatus Status,
    IdentityProfile? Profile = null)
{
    public bool IsSuccess => Status == ProfileResultStatus.Success && Profile is not null;

    public static ProfileOperationResult Success(IdentityProfile profile) =>
        new(ProfileResultStatus.Success, profile);

    public static ProfileOperationResult Failure(ProfileResultStatus status) =>
        new(status);
}

public enum PasswordChangeResultStatus
{
    Success,
    InvalidRequest,
    InvalidCredentials,
    NotFound,
}

public sealed record PasswordChangeOperationResult(PasswordChangeResultStatus Status)
{
    public bool IsSuccess => Status == PasswordChangeResultStatus.Success;
}

public sealed record LegadoTokenInfo(
    Guid Id,
    Guid UserId,
    string Name,
    string Prefix,
    LegadoTokenScope Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt);

public sealed record LegadoTokenIssue(
    LegadoTokenInfo Info,
    string RawToken);

public sealed record AuthenticatedLegadoToken(
    Guid UserId,
    Guid TokenId,
    LegadoTokenScope Scope);

public enum LegadoTokenResultStatus
{
    Success,
    InvalidRequest,
    NotFound,
}

public sealed record LegadoTokenOperationResult(
    LegadoTokenResultStatus Status,
    LegadoTokenIssue? Issue = null)
{
    public bool IsSuccess => Status == LegadoTokenResultStatus.Success && Issue is not null;

    public static LegadoTokenOperationResult Success(LegadoTokenIssue issue) =>
        new(LegadoTokenResultStatus.Success, issue);

    public static LegadoTokenOperationResult Failure(LegadoTokenResultStatus status) =>
        new(status);
}

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

    /// <summary>
    /// Persists a registration atomically; the first persisted user is an Administrator,
    /// and later registrations are Readers. Returns null for a duplicate email.
    /// </summary>
    Task<User?> AddRegistrationAsync(
        string normalizedEmail,
        string passwordHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在同一数据库事务内保存新密码并撤销该用户的全部 Web 会话。
    /// </summary>
    Task ChangePasswordAndRevokeSessionsAsync(
        User user,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
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

/// <summary>
/// Legado 个人令牌仓储。除按摘要验证外，所有管理操作均显式携带 UserId。
/// </summary>
public interface ILegadoAccessTokenRepository
{
    Task AddAsync(
        LegadoAccessToken token,
        CancellationToken cancellationToken = default);

    Task<LegadoAccessToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegadoAccessToken>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default);
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

    Task<IdentityProfile?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ProfileOperationResult> UpdateProfileAsync(
        Guid userId,
        string? displayName,
        CancellationToken cancellationToken = default);

    Task<PasswordChangeOperationResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public interface ILegadoAccessTokenService
{
    Task<LegadoTokenOperationResult> IssueAsync(
        Guid userId,
        string? name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegadoTokenInfo>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<LegadoTokenResultStatus> RevokeAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedLegadoToken?> ValidateAsync(
        string rawToken,
        LegadoTokenScope requiredScope,
        CancellationToken cancellationToken = default);
}
