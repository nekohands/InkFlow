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
    private readonly IIdentitySessionRepository _sessions;
    private readonly IPasswordHasher _passwords;
    private readonly IOpaqueTokenGenerator _tokens;
    private readonly TimeProvider _clock;
    private readonly IdentityOptions _options;

    public IdentityService(
        IUserRepository users,
        IIdentitySessionRepository sessions,
        IPasswordHasher passwords,
        IOpaqueTokenGenerator tokens,
        TimeProvider clock,
        IdentityOptions options)
    {
        _users = users;
        _sessions = sessions;
        _passwords = passwords;
        _tokens = tokens;
        _clock = clock;
        _options = options;
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
        var user = User.Create(normalizedEmail, _passwords.Hash(password), now);
        await _users.AddAsync(user, cancellationToken).ConfigureAwait(false);
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
        return rotated
            ? IdentityOperationResult.Success(replacement.Result)
            : IdentityOperationResult.Failure(IdentityResultStatus.InvalidRefreshToken);
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
        var token = await _sessions
            .FindAccessTokenAsync(OpaqueTokenHashing.Hash(accessToken), cancellationToken)
            .ConfigureAwait(false);
        if (token is null || !token.IsActive(now))
        {
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
