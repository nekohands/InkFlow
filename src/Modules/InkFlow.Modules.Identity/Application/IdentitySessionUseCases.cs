using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

/// <summary>访问令牌、refresh 轮换和会话撤销用例。</summary>
internal sealed class IdentitySessionUseCases
{
    private readonly IUserRepository _users;
    private readonly IIdentitySessionRepository _sessions;
    private readonly IOpaqueTokenGenerator _tokens;
    private readonly TimeProvider _clock;
    private readonly IdentityOptions _options;
    private readonly IIdentitySecurityEventSink? _securityEvents;

    public IdentitySessionUseCases(
        IUserRepository users,
        IIdentitySessionRepository sessions,
        IOpaqueTokenGenerator tokens,
        TimeProvider clock,
        IdentityOptions options,
        IIdentitySecurityEventSink? securityEvents)
    {
        _users = users;
        _sessions = sessions;
        _tokens = tokens;
        _clock = clock;
        _options = options;
        _securityEvents = securityEvents;
        _options.Validate();
    }

    public async Task<IdentityOperationResult> CreateSessionAsync(
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

    public async Task<IdentityOperationResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
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

    public async Task<AuthenticatedIdentity?> ValidateAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken)
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

    public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        return _sessions.RevokeSessionAsync(sessionId, _clock.GetUtcNow(), cancellationToken);
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
}
