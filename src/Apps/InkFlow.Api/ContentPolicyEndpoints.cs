using System.Diagnostics;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;

namespace InkFlow.Api;

public sealed record ContentPolicyTakedownRequest(Guid BookId, string? Reason);

public sealed record ContentPolicyRestoreRequest(string? Reason);

/// <summary>内容下架/恢复命令的结果适配与命令级审计。</summary>
public static class ContentPolicyEndpointResults
{
    public static IResult Command(
        ContentPolicyCommandResult result,
        ContentPolicyAction action,
        string actorId,
        string reason,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var decisionId = result.Decision?.Id;
        var reference = decisionId is { } id
            ? $"canonical-book:{result.CanonicalBookId};decision:{id}"
            : $"canonical-book:{result.CanonicalBookId}";
        var auditEvent = AuditEvent.Create(
            action: action == ContentPolicyAction.Takedown
                ? "content.policy.takedown"
                : "content.policy.restore",
            resource: $"/api/v1/admin/content/takedowns/{result.CanonicalBookId}",
            outcome: "success",
            statusCode: StatusCodes.Status200OK,
            occurredAt: clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: reference);

        return new AuditedResult(
            result,
            action,
            auditEvent,
            auditSink,
            cancellationToken);
    }

    private sealed class AuditedResult(
        ContentPolicyCommandResult result,
        ContentPolicyAction action,
        AuditEvent auditEvent,
        IAuditEventSink auditSink,
        CancellationToken cancellationToken) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await auditSink.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);

            await Results.Json(new
            {
                status = result.Changed ? "applied" : "already_in_state",
                action = action.ToString().ToLowerInvariant(),
                bookId = result.CanonicalBookId,
                isTakedown = result.IsTakedown,
                decisionId = result.Decision?.Id,
                decidedAt = result.Decision?.CreatedAt,
            }).ExecuteAsync(httpContext).ConfigureAwait(false);
        }
    }
}
