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
            health,
            action,
            auditEvent,
            auditSink,
            cancellationToken);
    }

    private sealed class AuditedResult(
        SourceCapabilityHealth health,
        SourceHealthCommandAction action,
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
                health = ToResponse(health),
            }).ExecuteAsync(httpContext).ConfigureAwait(false);
        }
    }
}
