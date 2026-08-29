using InkFlow.Modules.Developers.Domain;

namespace InkFlow.Modules.Developers.Application;

public enum DeveloperOperationStatus
{
    Success = 0,
    InvalidRequest = 1,
    NotFound = 2,
    LimitReached = 3,
    Conflict = 4,
}

public sealed record DeveloperApplicationView(
    Guid ApplicationId,
    Guid UserId,
    string Name,
    DeveloperEnvironment Environment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);

public sealed record DeveloperApiKeyView(
    Guid KeyId,
    Guid ApplicationId,
    string Name,
    string Prefix,
    string Scope,
    DeveloperEnvironment Environment,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

public sealed record IssuedDeveloperApiKey(
    DeveloperApiKeyView Key,
    string RawKey);

public sealed record DeveloperOperationResult<T>(
    DeveloperOperationStatus Status,
    T? Value = null)
    where T : class
{
    public bool IsSuccess => Status == DeveloperOperationStatus.Success && Value is not null;

    public static DeveloperOperationResult<T> Success(T value) =>
        new(DeveloperOperationStatus.Success, value);

    public static DeveloperOperationResult<T> Failure(DeveloperOperationStatus status) =>
        new(status);
}

public sealed record DeveloperKeyAuthentication(
    Guid UserId,
    Guid ApplicationId,
    Guid KeyId,
    string Scope,
    DeveloperEnvironment Environment);

public interface IDeveloperApplicationRepository
{
    /// <returns><see langword="true"/> when persisted; <see langword="false"/> when the active application cap is reached.</returns>
    Task<bool> AddAsync(DeveloperApplication application, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperApplication>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DeveloperApplication?> GetAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid userId,
        Guid applicationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface IDeveloperApiKeyRepository
{
    /// <returns><see langword="true"/> when persisted; <see langword="false"/> when the active key cap is reached.</returns>
    Task<bool> AddAsync(DeveloperApiKey key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperApiKey>> ListForApplicationAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default);

    Task<DeveloperApiKey?> GetAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        CancellationToken cancellationToken = default);

    Task<DeveloperApiKey?> FindByHashAsync(
        string secretHash,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> RotateAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        DeveloperApiKey replacement,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task MarkUsedAsync(
        Guid keyId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface IDeveloperUserStatusReader
{
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IDeveloperApiKeySecretGenerator
{
    DeveloperApiKeySecret Generate();
}

public sealed record DeveloperApiKeySecret(string RawKey, string Prefix, string SecretHash);

public interface IDeveloperApplicationService
{
    Task<DeveloperOperationResult<DeveloperApplicationView>> CreateApplicationAsync(
        Guid userId,
        string? name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperApplicationView>> ListApplicationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DeveloperOperationStatus> RevokeApplicationAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default);

    Task<DeveloperOperationResult<IssuedDeveloperApiKey>> IssueKeyAsync(
        Guid userId,
        Guid applicationId,
        string? name,
        int? expiresInDays,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeveloperApiKeyView>> ListKeysAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken = default);

    Task<DeveloperOperationResult<IssuedDeveloperApiKey>> RotateKeyAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        int? expiresInDays,
        CancellationToken cancellationToken = default);

    Task<DeveloperOperationStatus> RevokeKeyAsync(
        Guid userId,
        Guid applicationId,
        Guid keyId,
        CancellationToken cancellationToken = default);
}

public interface IDeveloperApiKeyValidator
{
    Task<DeveloperKeyAuthentication?> ValidateAsync(
        string rawKey,
        CancellationToken cancellationToken = default);
}

public static class DeveloperLimits
{
    public const int MaxApplicationsPerUser = 10;
    public const int MaxActiveKeysPerApplication = 5;
    public const int DefaultKeyLifetimeDays = 365;
    public const int MaxKeyLifetimeDays = 365;
    public const int MaxKeyNameLength = 128;
}

public sealed record DeveloperBookSummary(
    Guid BookId,
    string Title,
    string Author,
    int ChapterCount);

public sealed record DeveloperBookDetail(
    Guid BookId,
    string Title,
    string Author,
    int ChapterCount);

public sealed record DeveloperChapterSummary(
    Guid ChapterId,
    int Index,
    string Title);

public sealed record DeveloperChapterContent(
    Guid ChapterId,
    Guid BookId,
    int Index,
    string Title,
    IReadOnlyList<string> Paragraphs);
