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
    private readonly IPasswordHasher _passwords;
    private readonly TimeProvider _clock;
    private readonly IdentityAccountUseCases _account;
    private readonly IdentitySessionUseCases _session;

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
        _passwords = passwords;
        _clock = clock;
        _account = new(users, avatars, sessions, passwords, clock);
        _session = new(users, sessions, tokens, clock, options, securityEvents);
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

        return await _session
            .CreateSessionAsync(user, now, cancellationToken)
            .ConfigureAwait(false);
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

        return await _session
            .CreateSessionAsync(user, _clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IdentityOperationResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default) =>
        _session.RefreshAsync(refreshToken, cancellationToken);

    public Task<AuthenticatedIdentity?> ValidateAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        _session.ValidateAccessTokenAsync(accessToken, cancellationToken);

    public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => _session.LogoutAsync(sessionId, cancellationToken);

    public Task<IdentityProfile?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => _account.GetProfileAsync(userId, cancellationToken);

    public Task<ProfileOperationResult> UpdateProfileAsync(
        Guid userId,
        string? displayName,
        CancellationToken cancellationToken = default)
        => _account.UpdateProfileAsync(userId, displayName, cancellationToken);

    public Task<PasswordChangeOperationResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
        => _account.ChangePasswordAsync(userId, currentPassword, newPassword, cancellationToken);

    public Task<AvatarOperationResult> UploadAvatarAsync(
        Guid userId,
        Stream content,
        CancellationToken cancellationToken = default)
        => _account.UploadAvatarAsync(userId, content, cancellationToken);

    public Task<IdentityAvatar?> GetAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _account.GetAvatarAsync(userId, cancellationToken);

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
