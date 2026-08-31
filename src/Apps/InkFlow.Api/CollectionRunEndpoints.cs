using System.Diagnostics;
using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Api;

public sealed record StartCollectionRunRequest(string? Url);

public sealed record CollectionRunControlRequest(string? Action, string? Reason);

/// <summary>采集运行的受保护运维 API。</summary>
public static class CollectionRunEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var read = api.MapGroup("/admin/collection-runs")
            .RequireAuthorization(IdentityPolicies.OperationsRead);
        read.MapGet("", async (
            int? limit,
            CollectionRunService collectionRuns,
            CancellationToken ct) =>
        {
            var values = await collectionRuns
                .ListViewsAsync(Math.Clamp(limit ?? 50, 1, 100), ct)
                .ConfigureAwait(false);
            return Results.Ok(new { data = values.Select(ToResponse) });
        });
        read.MapGet("/{runId:guid}", async (
            Guid runId,
            CollectionRunService collectionRuns,
            CancellationToken ct) =>
        {
            var value = await collectionRuns.GetViewAsync(runId, ct).ConfigureAwait(false);
            return value is null
                ? Results.NotFound(new { error = "collection_run_not_found" })
                : Results.Ok(ToResponse(value));
        });

        var write = api.MapGroup("/admin/collection-runs")
            .RequireAuthorization(IdentityPolicies.CrawlerRepair);
        write.MapPost("", async (
            StartCollectionRunRequest? request,
            ClaimsPrincipal principal,
            CollectionRunService collectionRuns,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await collectionRuns
                .StartFromUrlAsync(request?.Url, ct)
                .ConfigureAwait(false);
            return StartAudited(
                result,
                actorId,
                httpContext,
                auditSink,
                clock,
                ct);
        });
        write.MapPost("/{runId:guid}/control", async (
            Guid runId,
            CollectionRunControlRequest? request,
            ClaimsPrincipal principal,
            CollectionRunService collectionRuns,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await collectionRuns
                .ControlAsync(runId, request?.Action, request?.Reason, ct)
                .ConfigureAwait(false);
            return ControlAudited(
                result,
                runId,
                request?.Action,
                request?.Reason,
                actorId,
                httpContext,
                auditSink,
                clock,
                ct);
        });
    }

    public static object ToResponse(CollectionRunView value) => new
    {
        id = value.Id,
        sourceId = value.SourceId,
        externalBookId = value.ExternalBookId,
        inputUrl = value.InputUrl,
        canonicalBookId = value.CanonicalBookId,
        status = value.Status.ToString().ToLowerInvariant(),
        stage = value.Stage switch
        {
            CollectionRunStage.BookInfo => "bookInfo",
            CollectionRunStage.Toc => "toc",
            CollectionRunStage.Content => "content",
            _ => value.Stage.ToString().ToLowerInvariant(),
        },
        totalTaskCount = value.TotalTaskCount,
        completedTaskCount = value.CompletedTaskCount,
        failedTaskCount = value.FailedTaskCount,
        pendingTaskCount = value.PendingTaskCount,
        inFlightTaskCount = value.InFlightTaskCount,
        cancelledTaskCount = value.CancelledTaskCount,
        remainingTaskCount = value.RemainingTaskCount,
        progressPercent = value.ProgressPercent,
        lastError = value.LastError,
        createdAt = value.CreatedAt,
        updatedAt = value.UpdatedAt,
    };

    public static int GetStartStatusCode(CollectionRunStartOutcome result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? result.Reused ? StatusCodes.Status200OK : StatusCodes.Status202Accepted
            : StatusCodes.Status422UnprocessableEntity;
    }

    private static IResult StartAudited(
        CollectionRunStartOutcome result,
        string actorId,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var statusCode = GetStartStatusCode(result);
        var audit = AuditEvent.Create(
            action: "collection.run.start",
            resource: "/api/v1/admin/collection-runs",
            outcome: result.IsSuccess ? "success" : "client_error",
            statusCode,
            clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
            reason: result.Error,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: result.Run is { } run
                ? $"run:{run.Id};reused:{result.Reused}"
                : $"error:{result.ErrorCode}");

        return new AuditedResult(
            result,
            statusCode,
            audit,
            auditSink,
            cancellationToken);
    }

    private static IResult ControlAudited(
        CollectionRunControlOutcome result,
        Guid runId,
        string? action,
        string? reason,
        string actorId,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var statusCode = result.IsSuccess
            ? StatusCodes.Status200OK
            : result.ErrorCode == "collection-run.not-found"
                ? StatusCodes.Status404NotFound
                : result.ErrorCode is "collection-run.invalid-state"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;
        var normalizedAction = action?.Trim().ToLowerInvariant() ?? "unknown";
        var audit = AuditEvent.Create(
            action: $"collection.run.{normalizedAction}",
            resource: $"/api/v1/admin/collection-runs/{runId}/control",
            outcome: result.IsSuccess ? "success" : "client_error",
            statusCode,
            clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: $"run:{runId};status:{result.Run?.Status.ToString() ?? result.ErrorCode}");

        return new AuditedResult(
            result,
            statusCode,
            audit,
            auditSink,
            cancellationToken);
    }

    private sealed class AuditedResult(
        object result,
        int statusCode,
        AuditEvent audit,
        IAuditEventSink auditSink,
        CancellationToken cancellationToken) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await auditSink.AppendAsync(audit, cancellationToken).ConfigureAwait(false);

            object payload = result switch
            {
                CollectionRunStartOutcome start when start.IsSuccess => new
                {
                    status = start.Reused ? "reused" : "accepted",
                    run = start.Run is null ? null : ToResponse(start.Run),
                },
                CollectionRunStartOutcome start => new
                {
                    error = start.ErrorCode ?? "collection_run_start_failed",
                },
                CollectionRunControlOutcome control when control.IsSuccess => new
                {
                    status = "applied",
                    run = control.Run is null ? null : ToResponse(control.Run),
                },
                CollectionRunControlOutcome control => new
                {
                    error = control.ErrorCode ?? "collection_run_control_failed",
                },
                _ => new { error = "collection_run_operation_failed" },
            };

            await Results.Json(payload, statusCode: statusCode)
                .ExecuteAsync(httpContext)
                .ConfigureAwait(false);
        }
    }
}
