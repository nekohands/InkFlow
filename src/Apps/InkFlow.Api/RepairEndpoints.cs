using System.Diagnostics;
using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Api;

public sealed record ReplayDeadLetterRequest(string? Reason);

public static class RepairEndpointResults
{
    public static IResult Replay(
        DeadLetterReplayResult result,
        Guid deadLetterId,
        string actorId,
        string reason,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var statusCode = result.Status switch
        {
            DeadLetterReplayStatus.Replayed or DeadLetterReplayStatus.AlreadyReplayed =>
                StatusCodes.Status200OK,
            DeadLetterReplayStatus.NotFound => StatusCodes.Status404NotFound,
            DeadLetterReplayStatus.OriginalTaskMissing or
            DeadLetterReplayStatus.OriginalTaskNotDeadLettered => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        var outcome = result.IsSuccess ? "success" : "client_error";
        var reference = result.ReplayTaskId is { } replayTaskId
            ? $"dead-letter:{deadLetterId};replay-task:{replayTaskId}"
            : $"dead-letter:{deadLetterId}";
        var auditEvent = AuditEvent.Create(
            action: "crawler.dead_letter.replay",
            resource: $"/api/v1/admin/crawler/dead-letters/{deadLetterId}/replay",
            outcome: outcome,
            statusCode: statusCode,
            occurredAt: clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
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

    public static bool TryGetActor(ClaimsPrincipal principal, out string actorId)
    {
        actorId = principal.FindFirstValue("sub") ?? string.Empty;
        return Guid.TryParse(actorId, out _);
    }

    private sealed class AuditedResult(
        DeadLetterReplayResult replay,
        int statusCode,
        AuditEvent auditEvent,
        IAuditEventSink auditSink,
        CancellationToken cancellationToken) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await auditSink.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);

            if (replay.IsSuccess)
            {
                await Results.Json(new
                {
                    status = replay.Status.ToString(),
                    replayTaskId = replay.ReplayTaskId,
                }, statusCode: statusCode).ExecuteAsync(httpContext).ConfigureAwait(false);
                return;
            }

            await Results.Json(new
            {
                error = replay.Status switch
                {
                    DeadLetterReplayStatus.NotFound => "dead_letter_not_found",
                    DeadLetterReplayStatus.OriginalTaskMissing => "original_task_not_found",
                    DeadLetterReplayStatus.OriginalTaskNotDeadLettered => "original_task_state_conflict",
                    _ => "repair_failed",
                },
            }, statusCode: statusCode).ExecuteAsync(httpContext).ConfigureAwait(false);
        }
    }
}
