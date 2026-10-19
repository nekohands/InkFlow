using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

internal static class ResourcePermissionRules
{
    public static bool IsAdministrator(Guid actorId, UserRole actorRole) =>
        actorId != Guid.Empty && actorRole == UserRole.Administrator;
}

internal sealed class ResourcePermissionCommands(
    IUserRepository users,
    IResourcePermissionRepository permissions,
    TimeProvider clock)
{
    public async Task<ResourcePermissionOperationResult> GrantAsync(
        Guid actorId,
        UserRole actorRole,
        Guid targetUserId,
        string? resourceType,
        string? resourceId,
        string? permission,
        CancellationToken cancellationToken = default)
    {
        if (!ResourcePermissionRules.IsAdministrator(actorId, actorRole))
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.ActorNotAllowed);
        }

        if (targetUserId == Guid.Empty ||
            !IdentityResourceAuthorization.TryNormalizeSource(
                resourceType,
                resourceId,
                out var normalizedResourceId) ||
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
        if (!ResourcePermissionRules.IsAdministrator(actorId, actorRole))
        {
            return ResourcePermissionOperationResult.Failure(
                ResourcePermissionResultStatus.ActorNotAllowed);
        }

        if (grantId == Guid.Empty ||
            !IdentityResourceAuthorization.TryNormalizeSource(
                resourceType,
                resourceId,
                out var normalizedResourceId))
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
}
