using InkFlow.Modules.Sources.Application;
using System.Diagnostics;
using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Api;

public sealed record SourcePermissionGrantRequest(
    Guid UserId,
    string? Permission,
    string? Reason);

/// <summary>来源级授权 API 的身份、输入和审计结果适配器。</summary>
public static class ResourcePermissionEndpointResults
{
    public static void Map(RouteGroupBuilder api)
    {
        var sourcePermissionManagement = api.MapGroup("/admin/sources")
            .RequireAuthorization(IdentityPolicies.PermissionManagement);

        sourcePermissionManagement.MapGet("/{sourceId}/permissions", async (
            string sourceId,
            int? limit,
            ClaimsPrincipal principal,
            ISourceRepository sources,
            IResourcePermissionService permissions,
            CancellationToken ct) =>
        {
            if (!SourceEndpointRules.IsValidSourceId(sourceId))
            {
                return (IResult)Results.BadRequest(new { error = "invalid_source_id" });
            }

            if (await sources.GetAsync(sourceId, ct).ConfigureAwait(false) is null)
            {
                return (IResult)Results.NotFound(new { error = "source_not_found" });
            }

            if (!ApiIdentity.TryGetIdentity(
                    principal,
                    out var actorId,
                    out var actorRole))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await permissions.ListAsync(
                actorId,
                actorRole,
                IdentityResourceTypes.Source,
                sourceId,
                limit ?? ResourcePermissionService.MaxListLimit,
                ct).ConfigureAwait(false);
            return result.Status switch
            {
                ResourcePermissionListStatus.Success =>
                    Results.Ok(result.Grants),
                ResourcePermissionListStatus.ActorNotAllowed =>
                    Results.Forbid(),
                _ => Results.BadRequest(new { error = "invalid_permission_request" }),
            };
        });

        sourcePermissionManagement.MapPost("/{sourceId}/permissions", (
            string sourceId,
            SourcePermissionGrantRequest? request,
            ClaimsPrincipal principal,
            ISourceRepository sources,
            IResourcePermissionService permissions,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) => HandleMutationAsync(
                sourceId,
                request?.Reason,
                principal,
                sources,
                (actorId, actorRole, cancellationToken) => permissions.GrantAsync(
                    actorId,
                    actorRole,
                    request!.UserId,
                    IdentityResourceTypes.Source,
                    sourceId,
                    request.Permission,
                    cancellationToken),
                "identity.resource_permission.grant",
                httpContext,
                auditSink,
                clock,
                ct));

        sourcePermissionManagement.MapDelete("/{sourceId}/permissions/{grantId:guid}", (
            string sourceId,
            Guid grantId,
            string? reason,
            ClaimsPrincipal principal,
            ISourceRepository sources,
            IResourcePermissionService permissions,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) => HandleMutationAsync(
                sourceId,
                reason,
                principal,
                sources,
                (actorId, actorRole, cancellationToken) => permissions.RevokeAsync(
                    actorId,
                    actorRole,
                    grantId,
                    IdentityResourceTypes.Source,
                    sourceId,
                    cancellationToken),
                "identity.resource_permission.revoke",
                httpContext,
                auditSink,
                clock,
                ct));
    }

    public const int MaxReasonLength = 512;

    public static bool TryGetIdentity(
        ClaimsPrincipal principal,
        out Guid userId,
        out UserRole role) =>
        ApiIdentity.TryGetIdentity(principal, out userId, out role);

    public static bool TryNormalizeReason(string? reason, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        var candidate = reason.Trim().Replace('\r', ' ').Replace('\n', ' ');
        if (candidate.Length == 0 ||
            candidate.Length > MaxReasonLength ||
            candidate.Any(char.IsControl))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    private static async Task<IResult> HandleMutationAsync(
        string sourceId,
        string? rawReason,
        ClaimsPrincipal principal,
        ISourceRepository sources,
        Func<Guid, UserRole, CancellationToken, Task<ResourcePermissionOperationResult>> mutation,
        string action,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!SourceEndpointRules.IsValidSourceId(sourceId) ||
            !TryNormalizeReason(rawReason, out var reason))
        {
            return Results.BadRequest(new { error = "invalid_permission_request" });
        }

        if (await sources.GetAsync(sourceId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Results.NotFound(new { error = "source_not_found" });
        }

        if (!ApiIdentity.TryGetIdentity(principal, out var actorId, out var actorRole))
        {
            return Results.Unauthorized();
        }

        var result = await mutation(actorId, actorRole, cancellationToken).ConfigureAwait(false);
        return AuditedOperation(
            result,
            action,
            sourceId,
            actorId,
            reason,
            httpContext,
            auditSink,
            clock,
            cancellationToken);
    }

    public static IResult AuditedOperation(
        ResourcePermissionOperationResult result,
        string action,
        string sourceId,
        Guid actorId,
        string reason,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var statusCode = StatusCodeFor(result.Status);
        var grant = result.Grant;
        var reference = grant is null
            ? $"source:{sourceId};status:{result.Status}"
            : $"source:{grant.ResourceId};grant:{grant.Id};" +
              $"target-user:{grant.UserId};permission:{grant.Permission}";
        var auditEvent = AuditEvent.Create(
            action,
            $"/api/v1/admin/sources/{Uri.EscapeDataString(sourceId)}/permissions",
            result.Status is ResourcePermissionResultStatus.Success or
                ResourcePermissionResultStatus.AlreadyGranted
                ? "success"
                : "client_error",
            statusCode,
            clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId.ToString("D"),
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: reference);

        return new AuditedResult(
            auditEvent,
            auditSink,
            cancellationToken,
            () => CreateResponse(result, statusCode));
    }

    public static int StatusCodeFor(ResourcePermissionResultStatus status) => status switch
    {
        ResourcePermissionResultStatus.Success => StatusCodes.Status200OK,
        ResourcePermissionResultStatus.AlreadyGranted => StatusCodes.Status200OK,
        ResourcePermissionResultStatus.InvalidRequest => StatusCodes.Status400BadRequest,
        ResourcePermissionResultStatus.ActorNotAllowed => StatusCodes.Status403Forbidden,
        ResourcePermissionResultStatus.TargetNotFound => StatusCodes.Status404NotFound,
        ResourcePermissionResultStatus.TargetNotEligible => StatusCodes.Status409Conflict,
        ResourcePermissionResultStatus.NotFound => StatusCodes.Status404NotFound,
        ResourcePermissionResultStatus.AlreadyRevoked => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string ErrorFor(ResourcePermissionResultStatus status) => status switch
    {
        ResourcePermissionResultStatus.InvalidRequest => "invalid_permission_request",
        ResourcePermissionResultStatus.ActorNotAllowed => "permission_management_forbidden",
        ResourcePermissionResultStatus.TargetNotFound => "target_user_not_found",
        ResourcePermissionResultStatus.TargetNotEligible => "target_user_not_eligible",
        ResourcePermissionResultStatus.NotFound => "permission_grant_not_found",
        ResourcePermissionResultStatus.AlreadyRevoked => "permission_grant_already_revoked",
        _ => "permission_operation_failed",
    };

    private static IResult CreateResponse(ResourcePermissionOperationResult result, int statusCode)
    {
        object payload = result.Grant is { } grant
            ? new
            {
                status = result.Status.ToString().ToLowerInvariant(),
                grant = new
                {
                    id = grant.Id,
                    userId = grant.UserId,
                    permission = grant.Permission,
                    resourceType = grant.ResourceType,
                    resourceId = grant.ResourceId,
                    grantedBy = grant.GrantedBy,
                    grantedAt = grant.GrantedAt,
                    revokedAt = grant.RevokedAt,
                    isActive = grant.IsActive,
                },
            }
            : new
            {
                error = ErrorFor(result.Status),
            };

        return Results.Json(payload, statusCode: statusCode);
    }
}
