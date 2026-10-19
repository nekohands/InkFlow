using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Identity.Application;
using System.Security.Claims;
using System.Diagnostics;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Api;

public sealed record SourceHealthCommandRequest(string? Reason);

public enum SourceHealthCommandAction
{
    Disable,
    Enable,
}

public sealed record SourceHealthResponse(
    string SourceId,
    string Capability,
    string Status,
    int ConsecutiveFailures,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    string? LastFailureReason,
    string AlgorithmVersion,
    DateTimeOffset UpdatedAt,
    bool IsAvailable);

/// <summary>来源健康运维结果适配器；命令审计在响应执行前追加写入。</summary>
public static class SourceHealthEndpointResults
{
    public static void Map(RouteGroupBuilder api)
    {
        var sourceOperationsRead = api.MapGroup("/admin/sources")
            .RequireAuthorization(IdentityPolicies.OperationsRead);

        sourceOperationsRead.MapGet("/{sourceId}/health", async (
            string sourceId,
            ClaimsPrincipal principal,
            ISourceRepository sources,
            ISourceHealthOperations health,
            IResourceAuthorizationService authorization,
            CancellationToken ct) =>
        {
            if (!SourceHealthEndpointResults.IsValidSourceId(sourceId))
            {
                return (IResult)Results.BadRequest(new { error = "invalid_source_id" });
            }

            if (await sources.GetAsync(sourceId, ct).ConfigureAwait(false) is null)
            {
                return (IResult)Results.NotFound(new { error = "source_not_found" });
            }

            if (!ApiIdentity.TryGetIdentity(
                    principal,
                    out var userId,
                    out var role))
            {
                return (IResult)Results.Unauthorized();
            }

            if (!await authorization.CanAccessAsync(
                    userId,
                    role,
                    IdentityPermissions.SourceRead,
                    IdentityResourceTypes.Source,
                    sourceId,
                    ct).ConfigureAwait(false))
            {
                return (IResult)Results.Forbid();
            }

            var rows = await health.ListForSourceAsync(sourceId, ct).ConfigureAwait(false);
            return Results.Ok(rows.Select(SourceHealthEndpointResults.ToResponse));
        });

        var sourceOperations = api.MapGroup("/admin/sources")
            .RequireAuthorization(IdentityPolicies.SourceOperations);

        sourceOperations.MapPost("/{sourceId}/health/{rawCapability}/disable", (
            string sourceId,
            string rawCapability,
            SourceHealthCommandRequest? request,
            ClaimsPrincipal principal,
            ISourceRepository sources,
            ISourceHealthOperations health,
            IResourceAuthorizationService authorization,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) => HandleCommandAsync(
                sourceId,
                rawCapability,
                request,
                principal,
                sources,
                health,
                authorization,
                SourceHealthCommandAction.Disable,
                httpContext,
                auditSink,
                clock,
                ct));

        sourceOperations.MapPost("/{sourceId}/health/{rawCapability}/enable", (
            string sourceId,
            string rawCapability,
            SourceHealthCommandRequest? request,
            ClaimsPrincipal principal,
            ISourceRepository sources,
            ISourceHealthOperations health,
            IResourceAuthorizationService authorization,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) => HandleCommandAsync(
                sourceId,
                rawCapability,
                request,
                principal,
                sources,
                health,
                authorization,
                SourceHealthCommandAction.Enable,
                httpContext,
                auditSink,
                clock,
                ct));
    }

    private static async Task<IResult> HandleCommandAsync(
        string sourceId,
        string rawCapability,
        SourceHealthCommandRequest? request,
        ClaimsPrincipal principal,
        ISourceRepository sources,
        ISourceHealthOperations health,
        IResourceAuthorizationService authorization,
        SourceHealthCommandAction action,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!ApiIdentity.TryGetIdentity(
                principal,
                out var actorUserId,
                out var actorRole))
        {
            return Results.Unauthorized();
        }
        var actorId = actorUserId.ToString("D");

        if (!SourceHealthEndpointResults.IsValidSourceId(sourceId) ||
            !SourceHealthEndpointResults.TryParseCapability(rawCapability, out var capability) ||
            request is null ||
            !SourceHealthEndpointResults.TryNormalizeReason(request.Reason, out var reason))
        {
            return Results.BadRequest(new { error = "invalid_source_health_request" });
        }

        if (await sources.GetAsync(sourceId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Results.NotFound(new { error = "source_not_found" });
        }

        if (!await authorization.CanAccessAsync(
                actorUserId,
                actorRole,
                IdentityPermissions.SourceManage,
                IdentityResourceTypes.Source,
                sourceId,
                cancellationToken).ConfigureAwait(false))
        {
            return Results.Forbid();
        }

        var updated = action == SourceHealthCommandAction.Disable
            ? await health.DisableAsync(sourceId, capability, reason, cancellationToken).ConfigureAwait(false)
            : await health.EnableAsync(sourceId, capability, cancellationToken).ConfigureAwait(false);
        return SourceHealthEndpointResults.Command(
            updated,
            action,
            actorId,
            reason,
            httpContext,
            auditSink,
            clock,
            cancellationToken);
    }

    public static bool IsValidSourceId(string sourceId) =>
        !string.IsNullOrWhiteSpace(sourceId) && !sourceId.Any(char.IsWhiteSpace);

    public static bool TryParseCapability(
        string rawCapability,
        out SourceCapability capability) =>
        Enum.TryParse(rawCapability, ignoreCase: true, out capability) &&
        Enum.IsDefined(capability);

    public static bool TryNormalizeReason(string? reason, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        var candidate = reason.Trim().Replace('\r', ' ').Replace('\n', ' ');
        if (candidate.Length == 0 || candidate.Length > SourceHealthPolicy.MaxFailureReasonLength)
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static SourceHealthResponse ToResponse(SourceCapabilityHealth health) => new(
        health.SourceId,
        health.Capability.ToString(),
        health.Status.ToString(),
        health.ConsecutiveFailures,
        health.LastSuccessAt,
        health.LastFailureAt,
        health.LastFailureReason,
        health.AlgorithmVersion,
        health.UpdatedAt,
        health.IsAvailable);

    public static IResult Command(
        SourceCapabilityHealth health,
        SourceHealthCommandAction action,
        string actorId,
        string reason,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var capability = health.Capability.ToString().ToLowerInvariant();
        var escapedSourceId = Uri.EscapeDataString(health.SourceId);
        var auditEvent = AuditEvent.Create(
            action: action == SourceHealthCommandAction.Disable
                ? "source.health.disable"
                : "source.health.enable",
            resource: $"/api/v1/admin/sources/{escapedSourceId}/health/{capability}",
            outcome: "success",
            statusCode: StatusCodes.Status200OK,
            occurredAt: clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: $"source:{health.SourceId};capability:{health.Capability};status:{health.Status}");

        return new AuditedResult(
            auditEvent,
            auditSink,
            cancellationToken,
            () => CreateResponse(health, action));
    }

    private static IResult CreateResponse(SourceCapabilityHealth health, SourceHealthCommandAction action)
    {
        return Results.Ok(new
        {
            status = "applied",
            action = action.ToString().ToLowerInvariant(),
            health = ToResponse(health),
        });
    }
}
