using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

/// <summary>账户资料、密码和头像用例；认证会话由 IdentityService 编排。</summary>
internal sealed class IdentityAccountUseCases
{
    private readonly IUserRepository _users;
    private readonly IUserAvatarRepository _avatars;
    private readonly IIdentitySessionRepository _sessions;
    private readonly IPasswordHasher _passwords;
    private readonly TimeProvider _clock;

    public IdentityAccountUseCases(
        IUserRepository users,
        IUserAvatarRepository avatars,
        IIdentitySessionRepository sessions,
        IPasswordHasher passwords,
        TimeProvider clock)
    {
        _users = users;
        _avatars = avatars;
        _sessions = sessions;
        _passwords = passwords;
        _clock = clock;
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
            currentPassword.Length > IdentityService.MaximumPasswordLength ||
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

    private static bool IsValidPassword(string password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length is >= IdentityService.MinimumPasswordLength and <= IdentityService.MaximumPasswordLength;

    private static IdentityProfile ToProfile(User user) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.Status,
            user.CreatedAt,
            user.UpdatedAt);
}
