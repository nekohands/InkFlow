using InkFlow.Modules.Developers.Domain;

namespace InkFlow.Modules.Developers.Application;

public sealed class DeveloperApplicationService(
    IDeveloperApplicationRepository applications,
    IDeveloperApiKeyRepository keys,
    IDeveloperApiKeySecretGenerator secrets,
    IDeveloperUserStatusReader users,
    TimeProvider clock) : IDeveloperApplicationService, IDeveloperApiKeyValidator
{
    private readonly DeveloperApiKeyUseCases apiKeys =
        new(applications, keys, secrets, users, clock);

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

    public Task<DeveloperOperationResult<IssuedDeveloperApiKey>> IssueKeyAsync(
        Guid userId,
        Guid applicationId,
        string? name,
        int? expiresInDays,
        CancellationToken cancellationToken = default) =>
        apiKeys.IssueKeyAsync(userId, applicationId, name, expiresInDays, cancellationToken);

    public Task<IReadOnlyList<DeveloperApiKeyView>> ListKeysAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default) =>
        apiKeys.ListKeysAsync(userId, applicationId, cancellationToken);

    public Task<DeveloperOperationResult<IssuedDeveloperApiKey>> RotateKeyAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        int? expiresInDays,
        CancellationToken cancellationToken = default) =>
        apiKeys.RotateKeyAsync(userId, applicationId, keyId, expiresInDays, cancellationToken);

    public Task<DeveloperOperationStatus> RevokeKeyAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        CancellationToken cancellationToken = default) =>
        apiKeys.RevokeKeyAsync(userId, applicationId, keyId, cancellationToken);

    public Task<DeveloperKeyAuthentication?> ValidateAsync(
        string rawKey,
        CancellationToken cancellationToken = default) =>
        apiKeys.ValidateAsync(rawKey, cancellationToken);

    private static DeveloperApplicationView ToView(DeveloperApplication application) =>
        new(
            application.Id,
            application.UserId,
            application.Name,
            application.Environment,
            application.CreatedAt,
            application.RevokedAt);

}
