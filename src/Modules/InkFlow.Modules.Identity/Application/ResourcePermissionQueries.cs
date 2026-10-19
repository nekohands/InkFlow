using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

internal sealed class ResourcePermissionQueries(IResourcePermissionRepository permissions)
{
    public async Task<ResourcePermissionListResult> ListAsync(
        Guid actorId,
        UserRole actorRole,
        string? resourceType,
        string? resourceId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!ResourcePermissionRules.IsAdministrator(actorId, actorRole))
        {
            return ResourcePermissionListResult.Failure(
                ResourcePermissionListStatus.ActorNotAllowed);
        }

        if (!IdentityResourceAuthorization.TryNormalizeSource(
                resourceType,
                resourceId,
                out var normalizedResourceId) ||
            limit is < 1 or > ResourcePermissionService.MaxListLimit)
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
