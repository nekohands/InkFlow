using InkFlow.Modules.Developers.Domain;

namespace InkFlow.Modules.Developers.Application;

public sealed class DeveloperApplicationService(
    IDeveloperApplicationRepository applications,
    IDeveloperApiKeyRepository keys,
    IDeveloperApiKeySecretGenerator secrets,
    IDeveloperUserStatusReader users,
    TimeProvider clock) : IDeveloperApplicationService, IDeveloperApiKeyValidator
{
    private const string DefaultKeyName = "Default key";

    public async Task<DeveloperOperationResult<DeveloperApplicationView>> CreateApplicationAsync(
        Guid userId,
        string? name,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !await users.IsActiveAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return DeveloperOperationResult<DeveloperApplicationView>.Failure(
                DeveloperOperationStatus.InvalidRequest);
        }

        try
        {
            var application = DeveloperApplication.Create(userId, name ?? string.Empty, clock.GetUtcNow());
            if (!await applications.AddAsync(application, cancellationToken).ConfigureAwait(false))
            {
                return DeveloperOperationResult<DeveloperApplicationView>.Failure(
                    DeveloperOperationStatus.LimitReached);
            }

            return DeveloperOperationResult<DeveloperApplicationView>.Success(ToView(application));
        }
        catch (ArgumentException)
        {
            return DeveloperOperationResult<DeveloperApplicationView>.Failure(
                DeveloperOperationStatus.InvalidRequest);
        }
    }

    public async Task<IReadOnlyList<DeveloperApplicationView>> ListApplicationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await applications.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return items.Select(ToView).ToList();
    }

    public async Task<DeveloperOperationStatus> RevokeApplicationAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default) =>
        await applications.RevokeAsync(
            userId,
            applicationId,
            clock.GetUtcNow(),
            cancellationToken).ConfigureAwait(false)
            ? DeveloperOperationStatus.Success
            : DeveloperOperationStatus.NotFound;

    public async Task<DeveloperOperationResult<IssuedDeveloperApiKey>> IssueKeyAsync(
        Guid userId,
        Guid applicationId,
        string? name,
        int? expiresInDays,
        CancellationToken cancellationToken = default)
    {
        var application = await applications.GetAsync(userId, applicationId, cancellationToken)
            .ConfigureAwait(false);
        if (application is null || !application.IsActive)
        {
            return DeveloperOperationResult<IssuedDeveloperApiKey>.Failure(
                DeveloperOperationStatus.NotFound);
        }

        if (!ValidateOptionalKeyName(name) || !TryLifetime(expiresInDays, out var lifetime))
        {
            return DeveloperOperationResult<IssuedDeveloperApiKey>.Failure(
                DeveloperOperationStatus.InvalidRequest);
        }

        return await CreateAndPersistKeyAsync(
                userId,
                application,
                name,
                lifetime,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeveloperApiKeyView>> ListKeysAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var application = await applications.GetAsync(userId, applicationId, cancellationToken)
            .ConfigureAwait(false);
        if (application is null)
        {
            return [];
        }

        var items = await keys.ListForApplicationAsync(userId, applicationId, cancellationToken)
            .ConfigureAwait(false);
        return items.Select(ToView).ToList();
    }

    public async Task<DeveloperOperationResult<IssuedDeveloperApiKey>> RotateKeyAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        int? expiresInDays,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var application = await applications.GetAsync(userId, applicationId, cancellationToken)
            .ConfigureAwait(false);
        var current = await keys.GetAsync(userId, applicationId, keyId, cancellationToken)
            .ConfigureAwait(false);
        if (application is null || !application.IsActive || current is null || current.RevokedAt is not null)
        {
            return DeveloperOperationResult<IssuedDeveloperApiKey>.Failure(
                DeveloperOperationStatus.NotFound);
        }

        if (!TryLifetime(expiresInDays, out var lifetime))
        {
            return DeveloperOperationResult<IssuedDeveloperApiKey>.Failure(
                DeveloperOperationStatus.InvalidRequest);
        }

        var secret = secrets.Generate();
        var replacement = DeveloperApiKey.Create(
            userId,
            applicationId,
            current.Name,
            secret.Prefix,
            secret.SecretHash,
            DeveloperApiScopes.CatalogRead,
            application.Environment,
            now,
            now.Add(lifetime));
        if (!await keys.RotateAsync(
                userId,
                applicationId,
                keyId,
                replacement,
                now,
                cancellationToken).ConfigureAwait(false))
        {
            return DeveloperOperationResult<IssuedDeveloperApiKey>.Failure(
                DeveloperOperationStatus.Conflict);
        }

        return DeveloperOperationResult<IssuedDeveloperApiKey>.Success(
            new IssuedDeveloperApiKey(ToView(replacement), secret.RawKey));
    }

    public async Task<DeveloperOperationStatus> RevokeKeyAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        CancellationToken cancellationToken = default) =>
        await keys.RevokeAsync(
            userId,
            applicationId,
            keyId,
            clock.GetUtcNow(),
            cancellationToken).ConfigureAwait(false)
            ? DeveloperOperationStatus.Success
            : DeveloperOperationStatus.NotFound;

    public async Task<DeveloperKeyAuthentication?> ValidateAsync(
        string rawKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey) || rawKey.Length > 512)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var key = await keys.FindByHashAsync(
                DeveloperApiKey.HashSecret(rawKey),
                cancellationToken)
            .ConfigureAwait(false);
        if (key is null || !key.IsActive(now) ||
            await applications.GetAsync(key.UserId, key.ApplicationId, cancellationToken)
                .ConfigureAwait(false) is not { IsActive: true } ||
            !await users.IsActiveAsync(key.UserId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        await keys.MarkUsedAsync(key.Id, now, cancellationToken).ConfigureAwait(false);
        return new DeveloperKeyAuthentication(
            key.UserId,
            key.ApplicationId,
            key.Id,
            key.Scope,
            key.Environment);
    }

    private async Task<DeveloperOperationResult<IssuedDeveloperApiKey>> CreateAndPersistKeyAsync(
        Guid userId,
        DeveloperApplication application,
        string? name,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var secret = secrets.Generate();
        var key = DeveloperApiKey.Create(
            userId,
            application.Id,
            string.IsNullOrWhiteSpace(name) ? DefaultKeyName : name.Trim(),
            secret.Prefix,
            secret.SecretHash,
            DeveloperApiScopes.CatalogRead,
            application.Environment,
            now,
            now.Add(lifetime));
        if (!await keys.AddAsync(key, cancellationToken).ConfigureAwait(false))
        {
            return DeveloperOperationResult<IssuedDeveloperApiKey>.Failure(
                DeveloperOperationStatus.LimitReached);
        }

        return DeveloperOperationResult<IssuedDeveloperApiKey>.Success(
            new IssuedDeveloperApiKey(ToView(key), secret.RawKey));
    }

    private static bool ValidateOptionalKeyName(string? name) =>
        name is null ||
        (name.Trim().Length is > 0 and <= DeveloperLimits.MaxKeyNameLength &&
         !name.Any(char.IsControl));

    private static bool TryLifetime(int? days, out TimeSpan lifetime)
    {
        var value = days ?? DeveloperLimits.DefaultKeyLifetimeDays;
        if (value is < 1 or > DeveloperLimits.MaxKeyLifetimeDays)
        {
            lifetime = default;
            return false;
        }

        lifetime = TimeSpan.FromDays(value);
        return true;
    }

    private static DeveloperApplicationView ToView(DeveloperApplication application) =>
        new(
            application.Id,
            application.UserId,
            application.Name,
            application.Environment,
            application.CreatedAt,
            application.RevokedAt);

    private static DeveloperApiKeyView ToView(DeveloperApiKey key) =>
        new(
            key.Id,
            key.ApplicationId,
            key.Name,
            key.Prefix,
            key.Scope,
            key.Environment,
            key.CreatedAt,
            key.ExpiresAt,
            key.LastUsedAt,
            key.RevokedAt);
}
