using InkFlow.Modules.Developers.Domain;

namespace InkFlow.Modules.Developers.Application;

public sealed class DeveloperApplicationService(
    IDeveloperApplicationRepository applications,
    IDeveloperApiKeyRepository keys,
    IDeveloperApiKeySecretGenerator secrets,
    IDeveloperUserStatusReader users,
    TimeProvider clock) : IDeveloperApplicationService, IDeveloperApiKeyValidator
{
    private readonly DeveloperApplicationCommands applicationCommands =
        new(applications, users, clock);
    private readonly DeveloperApplicationQueries applicationQueries =
        new(applications);
    private readonly DeveloperApiKeyUseCases apiKeys =
        new(applications, keys, secrets, users, clock);

    public Task<DeveloperOperationResult<DeveloperApplicationView>> CreateApplicationAsync(
        Guid userId,
        string? name,
        CancellationToken cancellationToken = default) =>
        applicationCommands.CreateAsync(userId, name, cancellationToken);

    public Task<IReadOnlyList<DeveloperApplicationView>> ListApplicationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        applicationQueries.ListAsync(userId, cancellationToken);

    public Task<DeveloperOperationStatus> RevokeApplicationAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default) =>
        applicationCommands.RevokeAsync(userId, applicationId, cancellationToken);

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

}
