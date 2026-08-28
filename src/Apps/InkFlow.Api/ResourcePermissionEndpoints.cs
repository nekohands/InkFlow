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

public sealed record SourcePermissionRevokeRequest(string? Reason);

/// <summary>来源级授权 API 的身份、输入和审计结果适配器。</summary>
public static class ResourcePermissionEndpointResults
{
    public const int MaxReasonLength = 512;

    public static bool TryGetIdentity(
        ClaimsPrincipal principal,
        out Guid userId,
        out UserRole role)
    {
        userId = Guid.Empty;
        role = default;
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out userId) ||
            userId == Guid.Empty)
        {
            return false;
        }

        var rawRole = principal.FindFirstValue(ClaimTypes.Role) ??
                      principal.FindFirstValue("role");
        return Enum.TryParse(rawRole, ignoreCase: true, out role) &&
               Enum.IsDefined(role);
    }

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
            result,
            statusCode,
            auditEvent,
            auditSink,
            cancellationToken);
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

    private sealed class AuditedResult(
        ResourcePermissionOperationResult result,
        int statusCode,
        AuditEvent auditEvent,
        IAuditEventSink auditSink,
        CancellationToken cancellationToken) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await auditSink.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);

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

            await Results.Json(payload, statusCode: statusCode)
                .ExecuteAsync(httpContext)
                .ConfigureAwait(false);
        }
    }
}
