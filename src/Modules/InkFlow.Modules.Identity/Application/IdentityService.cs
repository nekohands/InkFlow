using System.Security.Cryptography;
using System.Text;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

/// <summary>
/// Identity 用例编排：账号校验、短期访问令牌签发、refresh 轮换和会话撤销。
/// 调用方只依赖此小接口，不接触密码/令牌存储细节。
/// </summary>
public sealed class IdentityService : IIdentityService
{
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 256;

    private readonly IUserRepository _users;
    private readonly IUserAvatarRepository _avatars;
    private readonly IIdentitySessionRepository _sessions;
    private readonly IPasswordHasher _passwords;
    private readonly IOpaqueTokenGenerator _tokens;
    private readonly TimeProvider _clock;
    private readonly IdentityOptions _options;
    private readonly IIdentitySecurityEventSink? _securityEvents;

    public IdentityService(
        IUserRepository users,
        IUserAvatarRepository avatars,
        IIdentitySessionRepository sessions,
        IPasswordHasher passwords,
        IOpaqueTokenGenerator tokens,
        TimeProvider clock,
        IdentityOptions options,
        IIdentitySecurityEventSink? securityEvents = null)
    {
        _users = users;
        _avatars = avatars;
        _sessions = sessions;
        _passwords = passwords;
        _tokens = tokens;
        _clock = clock;
        _options = options;
        _securityEvents = securityEvents;
        _options.Validate();
    }

    public async Task<IdentityOperationResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeCredentials(email, password, out var normalizedEmail))
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidRequest);
        }

        if (await _users.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.EmailAlreadyRegistered);
        }

        var now = _clock.GetUtcNow();
        var user = await _users
            .AddRegistrationAsync(
                normalizedEmail,
                _passwords.Hash(password),
                now,
                cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.EmailAlreadyRegistered);
        }

        return await CreateSessionAsync(user, now, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IdentityOperationResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeEmail(email, out var normalizedEmail) || string.IsNullOrEmpty(password))
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidCredentials);
        }

        var user = await _users
            .FindByNormalizedEmailAsync(normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        if (user is null ||
            !user.CanAuthenticate ||
            !_passwords.Verify(password, user.PasswordHash))
        {
            // 不区分不存在、密码错误和停用状态，避免身份枚举。
            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidCredentials);
        }

        return await CreateSessionAsync(user, _clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IdentityOperationResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidRefreshToken);
        }

        var now = _clock.GetUtcNow();
        var currentHash = OpaqueTokenHashing.Hash(refreshToken);
        var current = await _sessions
            .FindRefreshSessionAsync(currentHash, cancellationToken)
            .ConfigureAwait(false);
        if (current is null || !current.IsActive(now))
        {
            // 已轮换的令牌再次出现即为重放：攻击者与合法持有者之一必然已失去同步。
            // 此时吊销整条令牌族，使双方都必须重新认证，而不是静默失败让攻击者继续持有会话。
            if (current is { IsRotated: true })
            {
                await RevokeReplayedFamilyAsync(current, now, cancellationToken).ConfigureAwait(false);
                return IdentityOperationResult.Failure(IdentityResultStatus.RefreshTokenReplayDetected);
            }

            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidRefreshToken);
        }

        var user = await _users.GetAsync(current.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.CanAuthenticate)
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidRefreshToken);
        }

        var replacement = CreateTokenPair(user, now);
        var rotated = await _sessions
            .RotateRefreshSessionAsync(
                currentHash,
                replacement.Session,
                replacement.AccessToken,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        if (!rotated)
        {
            // 竞态：另一并发请求已先完成轮换。按重放同样处理，吊销整族。
            var raced = await _sessions
                .FindRefreshSessionAsync(currentHash, cancellationToken)
                .ConfigureAwait(false);
            if (raced is { IsRotated: true })
            {
                await RevokeReplayedFamilyAsync(raced, now, cancellationToken).ConfigureAwait(false);
                return IdentityOperationResult.Failure(IdentityResultStatus.RefreshTokenReplayDetected);
            }

            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidRefreshToken);
        }

        return IdentityOperationResult.Success(replacement.Result);
    }

    private async Task RevokeReplayedFamilyAsync(
        RefreshSession replayedSession,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var revoked = await _sessions
            .RevokeSessionFamilyAsync(replayedSession.Id, now, cancellationToken)
            .ConfigureAwait(false);
        _securityEvents?.ReportRefreshReplay(replayedSession.UserId, revoked, now);
    }

    public async Task<AuthenticatedIdentity?> ValidateAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var now = _clock.GetUtcNow();
        var resolved = await _sessions
            .FindAccessTokenWithSessionAsync(OpaqueTokenHashing.Hash(accessToken), cancellationToken)
            .ConfigureAwait(false);
        if (resolved is not var (token, session) ||
            !token.IsActive(now) ||
            !session.IsActive(now))
        {
            // 访问令牌的有效性以所属会话活跃为前提：会话被吊销或已轮换时，
            // 即使访问令牌自身尚未过期也不再被接受，避免遗留幽灵凭证。
            return null;
        }

        var user = await _users.GetAsync(token.UserId, cancellationToken).ConfigureAwait(false);
        return user is null || !user.CanAuthenticate
            ? null
            : new AuthenticatedIdentity(user.Id, user.Email, user.Role, token.SessionId);
    }

    public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        return _sessions.RevokeSessionAsync(sessionId, _clock.GetUtcNow(), cancellationToken);
    }

    public async Task<IdentityProfile?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var user = await _users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? null : ToProfile(user);
    }

    public async Task<ProfileOperationResult> UpdateProfileAsync(
        Guid userId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return ProfileOperationResult.Failure(ProfileResultStatus.InvalidRequest);
        }

        var user = await _users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return ProfileOperationResult.Failure(ProfileResultStatus.NotFound);
        }

        try
        {
            user.UpdateDisplayName(displayName, _clock.GetUtcNow());
        }
        catch (ArgumentException)
        {
            return ProfileOperationResult.Failure(ProfileResultStatus.InvalidRequest);
        }

        await _users.SaveAsync(user, cancellationToken).ConfigureAwait(false);
        return ProfileOperationResult.Success(ToProfile(user));
    }

    public async Task<PasswordChangeOperationResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(currentPassword) ||
            currentPassword.Length > MaximumPasswordLength ||
            !IsValidPassword(newPassword))
        {
            return new PasswordChangeOperationResult(PasswordChangeResultStatus.InvalidRequest);
        }

        var user = await _users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.CanAuthenticate)
        {
            return new PasswordChangeOperationResult(PasswordChangeResultStatus.NotFound);
        }

        if (!_passwords.Verify(currentPassword, user.PasswordHash))
        {
            return new PasswordChangeOperationResult(PasswordChangeResultStatus.InvalidCredentials);
        }

        var now = _clock.GetUtcNow();
        user.ChangePasswordHash(_passwords.Hash(newPassword), now);
        await _users
            .ChangePasswordAndRevokeSessionsAsync(user, now, cancellationToken)
            .ConfigureAwait(false);
        return new PasswordChangeOperationResult(PasswordChangeResultStatus.Success);
    }

    public async Task<AvatarOperationResult> UploadAvatarAsync(
        Guid userId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return new AvatarOperationResult(AvatarResultStatus.InvalidRequest);
        }

        var user = await _users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.CanAuthenticate)
        {
            return new AvatarOperationResult(AvatarResultStatus.NotFound);
        }

        var image = await AvatarImage.ReadAsync(content, cancellationToken).ConfigureAwait(false);
        if (image is null)
        {
            return new AvatarOperationResult(AvatarResultStatus.InvalidRequest);
        }

        await _avatars.SaveAsync(
            new IdentityAvatar(userId, image.ContentType, image.Content, _clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return new AvatarOperationResult(AvatarResultStatus.Success);
    }

    public Task<IdentityAvatar?> GetAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        userId == Guid.Empty
            ? Task.FromResult<IdentityAvatar?>(null)
            : _avatars.GetAsync(userId, cancellationToken);

    private async Task<IdentityOperationResult> CreateSessionAsync(
        User user,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pair = CreateTokenPair(user, now);
        await _sessions
            .AddSessionAsync(pair.Session, pair.AccessToken, cancellationToken)
            .ConfigureAwait(false);
        return IdentityOperationResult.Success(pair.Result);
    }

    private (RefreshSession Session, AccessToken AccessToken, AuthSession Result) CreateTokenPair(
        User user,
        DateTimeOffset now)
    {
        var accessToken = _tokens.CreateToken();
        var refreshToken = _tokens.CreateToken();
        var session = RefreshSession.Create(
            user.Id,
            OpaqueTokenHashing.Hash(refreshToken),
            now,
            now + _options.RefreshTokenLifetime);
        var access = AccessToken.Create(
            user.Id,
            session.Id,
            OpaqueTokenHashing.Hash(accessToken),
            now,
            now + _options.AccessTokenLifetime);
        return (
            session,
            access,
            new AuthSession(
                session.Id,
                user.Id,
                user.Email,
                user.Role,
                accessToken,
                access.ExpiresAt,
                refreshToken,
                session.ExpiresAt));
    }

    private static bool TryNormalizeCredentials(
        string email,
        string password,
        out string normalizedEmail)
    {
        normalizedEmail = string.Empty;
        return TryNormalizeEmail(email, out normalizedEmail) &&
               !string.IsNullOrWhiteSpace(password) &&
               password.Length is >= MinimumPasswordLength and <= MaximumPasswordLength;
    }

    private static bool IsValidPassword(string password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length is >= MinimumPasswordLength and <= MaximumPasswordLength;

    private static IdentityProfile ToProfile(User user) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.Status,
            user.CreatedAt,
            user.UpdatedAt);

    private static bool TryNormalizeEmail(string email, out string normalizedEmail)
    {
        try
        {
            normalizedEmail = UserEmailAddress.Normalize(email);
            return true;
        }
        catch (ArgumentException)
        {
            normalizedEmail = string.Empty;
            return false;
        }
    }
}

public static class OpaqueTokenHashing
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
