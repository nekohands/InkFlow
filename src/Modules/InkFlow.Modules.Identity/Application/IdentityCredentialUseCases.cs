using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

/// <summary>注册与登录凭据用例；会话令牌创建仍由 IdentitySessionUseCases 负责。</summary>
internal sealed class IdentityCredentialUseCases(
    IUserRepository users,
    IPasswordHasher passwords,
    TimeProvider clock,
    IdentitySessionUseCases sessions)
{
    public async Task<IdentityOperationResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeCredentials(email, password, out var normalizedEmail))
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidRequest);
        }

        if (await users.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.EmailAlreadyRegistered);
        }

        var now = clock.GetUtcNow();
        var user = await users
            .AddRegistrationAsync(
                normalizedEmail,
                passwords.Hash(password),
                now,
                cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return IdentityOperationResult.Failure(IdentityResultStatus.EmailAlreadyRegistered);
        }

        return await sessions
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

        var user = await users
            .FindByNormalizedEmailAsync(normalizedEmail, cancellationToken)
            .ConfigureAwait(false);
        if (user is null ||
            !user.CanAuthenticate ||
            !passwords.Verify(password, user.PasswordHash))
        {
            // 不区分不存在、密码错误和停用状态，避免身份枚举。
            return IdentityOperationResult.Failure(IdentityResultStatus.InvalidCredentials);
        }

        return await sessions
            .CreateSessionAsync(user, clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryNormalizeCredentials(
        string email,
        string password,
        out string normalizedEmail)
    {
        normalizedEmail = string.Empty;
        return TryNormalizeEmail(email, out normalizedEmail) &&
               !string.IsNullOrWhiteSpace(password) &&
               password.Length is >= IdentityService.MinimumPasswordLength and <= IdentityService.MaximumPasswordLength;
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
