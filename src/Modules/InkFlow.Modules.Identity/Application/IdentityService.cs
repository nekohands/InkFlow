using System.Security.Cryptography;
using System.Text;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

/// <summary>
/// Identity 公开 facade：凭据、账户资料和会话用例的稳定入口。
/// 调用方只依赖此小接口，不接触密码/令牌存储细节。
/// </summary>
public sealed class IdentityService : IIdentityService
{
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 256;

    private readonly IdentityCredentialUseCases _credentials;
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
        _account = new(users, avatars, sessions, passwords, clock);
        _session = new(users, sessions, tokens, clock, options, securityEvents);
        _credentials = new(users, passwords, clock, _session);
    }

    public Task<IdentityOperationResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        _credentials.RegisterAsync(email, password, cancellationToken);

    public Task<IdentityOperationResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        _credentials.LoginAsync(email, password, cancellationToken);

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

}

public static class OpaqueTokenHashing
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
