using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

public static class IdentityResourceTypes
{
    public const string Source = "source";
}

public static class IdentityPermissions
{
    public const string SourceRead = "source.read";
    public const string SourceManage = "source.manage";

    public static bool TryNormalizeSourcePermission(
        string? raw,
        out string permission)
    {
        permission = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is not SourceRead and not SourceManage)
        {
            return false;
        }

        permission = normalized;
        return true;
    }
}

public static class IdentityResourceAuthorization
{
    public static bool TryNormalizeSourceId(string? raw, out string sourceId)
    {
        sourceId = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (normalized.Length > 256 ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        sourceId = normalized;
        return true;
    }

    public static bool TryNormalizeSource(
        string? resourceType,
        string? resourceId,
        out string normalizedResourceId)
    {
        normalizedResourceId = string.Empty;
        return IsSource(resourceType) &&
               TryNormalizeSourceId(resourceId, out normalizedResourceId);
    }

    public static bool IsSource(string? raw) =>
        string.Equals(raw?.Trim(), IdentityResourceTypes.Source, StringComparison.OrdinalIgnoreCase);
}

public enum ResourcePermissionResultStatus
{
    Success,
    AlreadyGranted,
    InvalidRequest,
    ActorNotAllowed,
    TargetNotFound,
    TargetNotEligible,
    NotFound,
    AlreadyRevoked,
}

public sealed record ResourcePermissionGrantInfo(
    Guid Id,
    Guid UserId,
    string Permission,
    string ResourceType,
    string ResourceId,
    Guid GrantedBy,
    DateTimeOffset GrantedAt,
    DateTimeOffset? RevokedAt)
{
    public bool IsActive => RevokedAt is null;

    public static ResourcePermissionGrantInfo FromDomain(PermissionGrant grant) => new(
        grant.Id,
        grant.UserId,
        grant.Permission,
        grant.ResourceType,
        grant.ResourceId,
        grant.GrantedBy,
        grant.GrantedAt,
        grant.RevokedAt);
}

public sealed record ResourcePermissionOperationResult(
    ResourcePermissionResultStatus Status,
    ResourcePermissionGrantInfo? Grant = null)
{
    public static ResourcePermissionOperationResult Success(PermissionGrant grant) =>
        new(ResourcePermissionResultStatus.Success, ResourcePermissionGrantInfo.FromDomain(grant));

    public static ResourcePermissionOperationResult AlreadyGranted(PermissionGrant grant) =>
        new(ResourcePermissionResultStatus.AlreadyGranted, ResourcePermissionGrantInfo.FromDomain(grant));

    public static ResourcePermissionOperationResult Failure(ResourcePermissionResultStatus status) =>
        new(status);
}

public enum ResourcePermissionListStatus
{
    Success,
    InvalidRequest,
    ActorNotAllowed,
}

public sealed record ResourcePermissionListResult(
    ResourcePermissionListStatus Status,
    IReadOnlyList<ResourcePermissionGrantInfo> Grants)
{
    public static ResourcePermissionListResult Success(
        IReadOnlyList<PermissionGrant> grants) =>
        new(
            ResourcePermissionListStatus.Success,
            grants.Select(ResourcePermissionGrantInfo.FromDomain).ToList());

    public static ResourcePermissionListResult Failure(ResourcePermissionListStatus status) =>
        new(status, []);
}

public interface IResourcePermissionRepository
{
    Task<PermissionGrant?> GetAsync(
        Guid grantId,
        CancellationToken cancellationToken = default);

    Task<PermissionGrant?> FindActiveAsync(
        Guid userId,
        string resourceType,
        string resourceId,
        string permission,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionGrant>> ListActiveForResourceAsync(
        string resourceType,
        string resourceId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionGrant>> ListActiveForUserResourceAsync(
        Guid userId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionGrant>> ListActiveForUserAsync(
        Guid userId,
        string resourceType,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PermissionGrant grant,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        PermissionGrant grant,
        CancellationToken cancellationToken = default);
}

public interface IResourcePermissionService
{
    Task<ResourcePermissionOperationResult> GrantAsync(
        Guid actorId,
        UserRole actorRole,
        Guid targetUserId,
        string? resourceType,
        string? resourceId,
        string? permission,
        CancellationToken cancellationToken = default);

    Task<ResourcePermissionOperationResult> RevokeAsync(
        Guid actorId,
        UserRole actorRole,
        Guid grantId,
        string? resourceType,
        string? resourceId,
        CancellationToken cancellationToken = default);

    Task<ResourcePermissionListResult> ListAsync(
        Guid actorId,
        UserRole actorRole,
        string? resourceType,
        string? resourceId,
        int limit,
        CancellationToken cancellationToken = default);
}

public interface IResourceAuthorizationService
{
    Task<bool> CanAccessAsync(
        Guid userId,
        UserRole role,
        string? permission,
        string? resourceType,
        string? resourceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> ListAllowedResourceIdsAsync(
        Guid userId,
        UserRole role,
        string? permission,
        string? resourceType,
        CancellationToken cancellationToken = default);
}

public sealed class ResourcePermissionService : IResourcePermissionService
{
    public const int MaxListLimit = 100;

    private readonly ResourcePermissionCommands commands;
    private readonly ResourcePermissionQueries queries;

    public ResourcePermissionService(
        IUserRepository users,
        IResourcePermissionRepository permissions,
        TimeProvider clock)
    {
        commands = new(users, permissions, clock);
        queries = new(permissions);
    }

    public Task<ResourcePermissionOperationResult> GrantAsync(
        Guid actorId,
        UserRole actorRole,
        Guid targetUserId,
        string? resourceType,
        string? resourceId,
        string? permission,
        CancellationToken cancellationToken = default) =>
        commands.GrantAsync(
            actorId,
            actorRole,
            targetUserId,
            resourceType,
            resourceId,
            permission,
            cancellationToken);

    public Task<ResourcePermissionOperationResult> RevokeAsync(
        Guid actorId,
        UserRole actorRole,
        Guid grantId,
        string? resourceType,
        string? resourceId,
        CancellationToken cancellationToken = default) =>
        commands.RevokeAsync(
            actorId,
            actorRole,
            grantId,
            resourceType,
            resourceId,
            cancellationToken);

    public Task<ResourcePermissionListResult> ListAsync(
        Guid actorId,
        UserRole actorRole,
        string? resourceType,
        string? resourceId,
        int limit,
        CancellationToken cancellationToken = default) =>
        queries.ListAsync(
            actorId,
            actorRole,
            resourceType,
            resourceId,
            limit,
            cancellationToken);
}

public sealed class ResourceAuthorizationService(
    IResourcePermissionRepository permissions) : IResourceAuthorizationService
{
    private const int MaxAuthorizationResourceIds = 1_000;

    public async Task<bool> CanAccessAsync(
        Guid userId,
        UserRole role,
        string? permission,
        string? resourceType,
        string? resourceId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty ||
            !IdentityResourceAuthorization.TryNormalizeSource(
                resourceType,
                resourceId,
                out var normalizedResourceId) ||
            !IdentityPermissions.TryNormalizeSourcePermission(permission, out var normalizedPermission))
        {
            return false;
        }

        if (role == UserRole.Administrator)
        {
            return true;
        }

        if (role != UserRole.Operator)
        {
            return false;
        }

        var grants = await permissions.ListActiveForUserResourceAsync(
            userId,
            IdentityResourceTypes.Source,
            normalizedResourceId,
            cancellationToken).ConfigureAwait(false);
        return grants.Any(grant => IsSufficient(grant.Permission, normalizedPermission));
    }

    public async Task<IReadOnlySet<string>> ListAllowedResourceIdsAsync(
        Guid userId,
        UserRole role,
        string? permission,
        string? resourceType,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty ||
            role != UserRole.Operator ||
            !IdentityResourceAuthorization.IsSource(resourceType) ||
            !IdentityPermissions.TryNormalizeSourcePermission(permission, out var normalizedPermission))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var grants = await permissions.ListActiveForUserAsync(
            userId,
            IdentityResourceTypes.Source,
            MaxAuthorizationResourceIds,
            cancellationToken).ConfigureAwait(false);
        return grants
            .Where(grant => IsSufficient(grant.Permission, normalizedPermission))
            .Select(grant => grant.ResourceId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsSufficient(string grantedPermission, string requiredPermission) =>
        string.Equals(grantedPermission, requiredPermission, StringComparison.Ordinal) ||
        string.Equals(grantedPermission, IdentityPermissions.SourceManage, StringComparison.Ordinal) &&
        string.Equals(requiredPermission, IdentityPermissions.SourceRead, StringComparison.Ordinal);
}
