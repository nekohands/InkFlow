using System.Security.Claims;
using InkFlow.Modules.Identity.Application;
using System.Diagnostics;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Api;

public sealed record SourceCredentialBindingRequest(
    string? CredentialReferenceId,
    string? Reason);

public sealed record SourceCredentialBindingResponse(
    string SourceId,
    string Status,
    string? CredentialReferenceId);

public enum SourceCredentialBindingCommandAction
{
    Set,
    Clear,
}

/// <summary>来源默认凭据引用管理 API 的输入、响应和审计适配器。</summary>
public static class SourceCredentialBindingEndpointResults
{
    public static void Map(RouteGroupBuilder api)
    {
        var sourceCredentialManagement = api.MapGroup("/admin/sources")
            .RequireAuthorization(IdentityPolicies.SourceCredentialManagement);

        sourceCredentialManagement.MapPut("/{sourceId}/credential-binding", async (
            string sourceId,
            SourceCredentialBindingRequest? request,
            ClaimsPrincipal principal,
            ISourceCredentialBindingService service,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!SourceCredentialBindingEndpointResults.TryNormalizeSourceId(
                    sourceId,
                    out var normalizedSourceId) ||
                request is null ||
                !SourceCredentialBindingEndpointResults.TryNormalizeReason(
                    request.Reason,
                    out var reason))
            {
                return (IResult)Results.BadRequest(new { error = "invalid_credential_binding_request" });
            }

            if (!ResourcePermissionEndpointResults.TryGetIdentity(
                    principal,
                    out var actorId,
                    out _))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await service.SetDefaultAsync(
                normalizedSourceId,
                request.CredentialReferenceId,
                ct).ConfigureAwait(false);
            return SourceCredentialBindingEndpointResults.Command(
                result,
                request.CredentialReferenceId is null
                    ? SourceCredentialBindingCommandAction.Clear
                    : SourceCredentialBindingCommandAction.Set,
                actorId.ToString("D"),
                reason,
                httpContext,
                auditSink,
                clock,
                ct);
        });
    }

    public const int MaxSourceIdLength = 128;
    public const int MaxReasonLength = 512;

    public static bool TryNormalizeSourceId(string? sourceId, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return false;
        }

        var candidate = sourceId.Trim();
        if (candidate.Length == 0 ||
            candidate.Length > MaxSourceIdLength ||
            candidate.Any(character =>
                char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        normalized = candidate;
        return true;
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

    public static SourceCredentialBindingResponse ToResponse(
        SourceCredentialBindingOperationResult result) =>
        new(
            result.SourceId,
            StatusText(result.Status),
            result.CredentialReferenceId);

    public static IResult Command(
        SourceCredentialBindingOperationResult result,
        SourceCredentialBindingCommandAction action,
        string actorId,
        string reason,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var escapedSourceId = Uri.EscapeDataString(result.SourceId);
        var auditEvent = AuditEvent.Create(
            action: action == SourceCredentialBindingCommandAction.Set
                ? "source.credential_binding.set"
                : "source.credential_binding.clear",
            resource:
                $"/api/v1/admin/sources/{Uri.EscapeDataString(result.SourceId)}/credential-binding",
            outcome: result.Status is
                SourceCredentialBindingResultStatus.Updated or
                SourceCredentialBindingResultStatus.Cleared
                ? "success"
                : "client_error",
            statusCode: StatusCodeFor(result.Status),
            occurredAt: clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: $"source:{escapedSourceId};binding:{StatusText(result.Status)}");

        return new AuditedResult(
            auditEvent,
            auditSink,
            cancellationToken,
            () => CreateResponse(result));
    }

    public static int StatusCodeFor(SourceCredentialBindingResultStatus status) => status switch
    {
        SourceCredentialBindingResultStatus.Updated or
        SourceCredentialBindingResultStatus.Cleared => StatusCodes.Status200OK,
        SourceCredentialBindingResultStatus.SourceNotFound => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string StatusText(SourceCredentialBindingResultStatus status) => status switch
    {
        SourceCredentialBindingResultStatus.Updated => "updated",
        SourceCredentialBindingResultStatus.Cleared => "cleared",
        SourceCredentialBindingResultStatus.SourceNotFound => "not_found",
        _ => "invalid_request",
    };

    private static IResult CreateResponse(SourceCredentialBindingOperationResult result)
    {
        var response = result.Status switch
        {
            SourceCredentialBindingResultStatus.Updated or
            SourceCredentialBindingResultStatus.Cleared =>
                Results.Ok(ToResponse(result)),
            SourceCredentialBindingResultStatus.SourceNotFound =>
                Results.NotFound(new { error = "source_not_found" }),
            _ => Results.BadRequest(new { error = "invalid_credential_binding_request" }),
        };
        return response;
    }
}
