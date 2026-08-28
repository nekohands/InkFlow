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

public sealed class ResourcePermissionService(
    IUserRepository users,
    IResourcePermissionRepository permissions,
    TimeProvider clock) : IResourcePermissionService
{
    public const int MaxListLimit = 100;

    public async Task<ResourcePermissionOperationResult> GrantAsync(
        Guid actorId,
        UserRole actorRole,
        Guid targetUserId,
        string? resourceType,
        string? resourceId,
        string? permission,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || actorRole != UserRole.Administrator)
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.ActorNotAllowed);
        }

        if (targetUserId == Guid.Empty ||
            !IdentityResourceAuthorization.IsSource(resourceType) ||
            !IdentityResourceAuthorization.TryNormalizeSourceId(resourceId, out var normalizedResourceId) ||
            !IdentityPermissions.TryNormalizeSourcePermission(permission, out var normalizedPermission))
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.InvalidRequest);
        }

        var target = await users.GetAsync(targetUserId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.TargetNotFound);
        }

        if (!target.CanAuthenticate || target.Role != UserRole.Operator)
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.TargetNotEligible);
        }

        var existing = await permissions.FindActiveAsync(
            targetUserId,
            IdentityResourceTypes.Source,
            normalizedResourceId,
            normalizedPermission,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return ResourcePermissionOperationResult.AlreadyGranted(existing);
        }

        var grant = PermissionGrant.Create(
            targetUserId,
            normalizedPermission,
            IdentityResourceTypes.Source,
            normalizedResourceId,
            actorId,
            clock.GetUtcNow());
        await permissions.AddAsync(grant, cancellationToken).ConfigureAwait(false);
        return ResourcePermissionOperationResult.Success(grant);
    }

    public async Task<ResourcePermissionOperationResult> RevokeAsync(
        Guid actorId,
        UserRole actorRole,
        Guid grantId,
        string? resourceType,
        string? resourceId,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || actorRole != UserRole.Administrator)
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.ActorNotAllowed);
        }

        if (grantId == Guid.Empty ||
            !IdentityResourceAuthorization.IsSource(resourceType) ||
            !IdentityResourceAuthorization.TryNormalizeSourceId(resourceId, out var normalizedResourceId))
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.InvalidRequest);
        }

        var grant = await permissions.GetAsync(grantId, cancellationToken).ConfigureAwait(false);
        if (grant is null ||
            !string.Equals(grant.ResourceType, IdentityResourceTypes.Source, StringComparison.Ordinal) ||
            !string.Equals(grant.ResourceId, normalizedResourceId, StringComparison.Ordinal))
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.NotFound);
        }

        if (!grant.IsActive)
        {
            return new ResourcePermissionOperationResult(
                ResourcePermissionResultStatus.AlreadyRevoked,
                ResourcePermissionGrantInfo.FromDomain(grant));
        }

        grant.Revoke(clock.GetUtcNow());
        await permissions.SaveAsync(grant, cancellationToken).ConfigureAwait(false);
        return ResourcePermissionOperationResult.Success(grant);
    }

    public async Task<ResourcePermissionListResult> ListAsync(
        Guid actorId,
        UserRole actorRole,
        string? resourceType,
        string? resourceId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || actorRole != UserRole.Administrator)
        {
            return ResourcePermissionListResult.Failure(
                ResourcePermissionListStatus.ActorNotAllowed);
        }

        if (!IdentityResourceAuthorization.IsSource(resourceType) ||
            !IdentityResourceAuthorization.TryNormalizeSourceId(resourceId, out var normalizedResourceId) ||
            limit is < 1 or > MaxListLimit)
        {
            return ResourcePermissionListResult.Failure(
                ResourcePermissionListStatus.InvalidRequest);
        }

        var grants = await permissions.ListActiveForResourceAsync(
            IdentityResourceTypes.Source,
            normalizedResourceId,
            limit,
            cancellationToken).ConfigureAwait(false);
        return ResourcePermissionListResult.Success(grants);
    }
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
            !IdentityResourceAuthorization.IsSource(resourceType) ||
            !IdentityResourceAuthorization.TryNormalizeSourceId(resourceId, out var normalizedResourceId) ||
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
