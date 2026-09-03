using System.Diagnostics;
using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Api;

public sealed record SourceLifecycleCommandRequest(string? Reason);

public enum SourceLifecycleCommandAction
{
    Disable,
    Enable,
}

/// <summary>来源级启停 API；停用不删除来源数据，并保留操作审计。</summary>
public static class SourceLifecycleEndpointResults
{
    public static void Map(RouteGroupBuilder api)
    {
        var operations = api.MapGroup("/admin/sources")
            .RequireAuthorization(IdentityPolicies.SourceOperations);

        operations.MapPost("/{sourceId}/disable", (
            string sourceId,
            SourceLifecycleCommandRequest? request,
            ClaimsPrincipal principal,
            ISourceRepository sources,
            SourceLifecycleService lifecycle,
            IResourceAuthorizationService authorization,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) => HandleAsync(
                sourceId,
                request,
                principal,
                enabled: false,
                sources,
                lifecycle,
                authorization,
                httpContext,
                auditSink,
                clock,
                ct));

        operations.MapPost("/{sourceId}/enable", (
            string sourceId,
            SourceLifecycleCommandRequest? request,
            ClaimsPrincipal principal,
            ISourceRepository sources,
            SourceLifecycleService lifecycle,
            IResourceAuthorizationService authorization,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) => HandleAsync(
                sourceId,
                request,
                principal,
                enabled: true,
                sources,
                lifecycle,
                authorization,
                httpContext,
                auditSink,
                clock,
                ct));
    }

    public static IResult Command(
        Source source,
        SourceLifecycleCommandAction action,
        string actorId,
        string reason,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var verb = action == SourceLifecycleCommandAction.Disable ? "disable" : "enable";
        var escapedSourceId = Uri.EscapeDataString(source.Id);
        var auditEvent = AuditEvent.Create(
            action: $"source.{verb}",
            resource: $"/api/v1/admin/sources/{escapedSourceId}/{verb}",
            outcome: "success",
            statusCode: StatusCodes.Status200OK,
            occurredAt: clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: $"source:{source.Id};enabled:{source.IsEnabled}");

        return new AuditedResult(source, action, auditEvent, auditSink, cancellationToken);
    }

    private static async Task<IResult> HandleAsync(
        string sourceId,
        SourceLifecycleCommandRequest? request,
        ClaimsPrincipal principal,
        bool enabled,
        ISourceRepository sources,
        SourceLifecycleService lifecycle,
        IResourceAuthorizationService authorization,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!ResourcePermissionEndpointResults.TryGetIdentity(
                principal,
                out var actorUserId,
                out var actorRole))
        {
            return Results.Unauthorized();
        }

        if (!SourceHealthEndpointResults.IsValidSourceId(sourceId) ||
            request is null ||
            !ResourcePermissionEndpointResults.TryNormalizeReason(request.Reason, out var reason))
        {
            return Results.BadRequest(new { error = "invalid_source_lifecycle_request" });
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

        var updated = await lifecycle
            .SetEnabledAsync(sourceId, enabled, cancellationToken)
            .ConfigureAwait(false);
        return updated is null
            ? Results.NotFound(new { error = "source_not_found" })
            : Command(
                updated,
                enabled ? SourceLifecycleCommandAction.Enable : SourceLifecycleCommandAction.Disable,
                actorUserId.ToString("D"),
                reason,
                httpContext,
                auditSink,
                clock,
                cancellationToken);
    }

    private sealed class AuditedResult(
        Source source,
        SourceLifecycleCommandAction action,
        AuditEvent auditEvent,
        IAuditEventSink auditSink,
        CancellationToken cancellationToken) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await auditSink.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);

            await Results.Ok(new
            {
                status = "applied",
                action = action.ToString().ToLowerInvariant(),
                source = new
                {
                    sourceId = source.Id,
                    displayName = source.DisplayName,
                    isEnabled = source.IsEnabled,
                    updatedAt = source.UpdatedAt,
                },
            }).ExecuteAsync(httpContext).ConfigureAwait(false);
        }
    }
}
