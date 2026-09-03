using System.Globalization;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Identity.Application;
using Microsoft.AspNetCore.WebUtilities;

namespace InkFlow.Api;

public sealed record StartCollectionRunRequest(string? Url);

public sealed record CollectionRunControlRequest(string? Action, string? Reason);

public sealed record CollectionRunDeleteRequest(string? Reason);

/// <summary>采集运行的受保护运维 API。</summary>
public static class CollectionRunEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var read = api.MapGroup("/admin/collection-runs")
            .RequireAuthorization(IdentityPolicies.OperationsRead);
        read.MapGet("", async (
            int? limit,
            string? cursor,
            CollectionRunService collectionRuns,
            CancellationToken ct) =>
        {
            if (!TryCreateQuery(limit, cursor, out var safeLimit, out var before, out var error))
            {
                return (IResult)Results.BadRequest(new { error });
            }

            var page = await collectionRuns
                .ListPageViewsAsync(safeLimit, before, ct)
                .ConfigureAwait(false);
            return Results.Ok(new
            {
                data = page.Entries.Select(ToResponse),
                nextCursor = EncodeCursor(page.NextCursor),
            });
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
        write.MapPost("/{runId:guid}/delete", async (
            Guid runId,
            CollectionRunDeleteRequest? request,
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
                .DeleteFailedAsync(runId, request?.Reason, ct)
                .ConfigureAwait(false);
            return DeleteAudited(
                result,
                runId,
                request?.Reason,
                actorId,
                httpContext,
                auditSink,
                clock,
                ct);
        });
    }

    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public static bool TryCreateQuery(
        int? limitRaw,
        string? cursorRaw,
        out int limit,
        out CollectionRunCursor? before,
        out string error)
    {
        limit = limitRaw ?? DefaultLimit;
        before = null;
        error = "invalid_collection_run_query";
        return limit is >= 1 and <= MaxLimit && TryParseCursor(cursorRaw, out before);
    }

    public static string? EncodeCursor(CollectionRunCursor? cursor)
    {
        if (cursor is null)
        {
            return null;
        }

        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{cursor.UpdatedAt.ToUniversalTime():O}|{cursor.Id:D}");
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
    }

    private static bool TryParseCursor(
        string? raw,
        out CollectionRunCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (raw.Length > 256)
        {
            return false;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(raw.Trim()));
            var separator = payload.IndexOf('|');
            if (separator <= 0 || separator == payload.Length - 1 ||
                payload.IndexOf('|', separator + 1) >= 0)
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(
                    payload[..separator],
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var updatedAt) ||
                !Guid.TryParseExact(payload[(separator + 1)..], "D", out var id) ||
                id == Guid.Empty)
            {
                return false;
            }

            cursor = new CollectionRunCursor(updatedAt.ToUniversalTime(), id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
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

        if (result.IsSuccess)
        {
            return result.Reused ? StatusCodes.Status200OK : StatusCodes.Status202Accepted;
        }

        return result.ErrorCode is "source-url.empty" or "source-url.invalid"
            ? StatusCodes.Status400BadRequest
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

    public static int GetDeleteStatusCode(CollectionRunDeleteOutcome result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? StatusCodes.Status200OK
            : result.ErrorCode == "collection-run.not-found"
                ? StatusCodes.Status404NotFound
                : result.ErrorCode == "collection-run.not-failed"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;
    }

    private static IResult DeleteAudited(
        CollectionRunDeleteOutcome result,
        Guid runId,
        string? reason,
        string actorId,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var statusCode = GetDeleteStatusCode(result);
        var audit = AuditEvent.Create(
            action: "collection.run.delete",
            resource: $"/api/v1/admin/collection-runs/{runId}/delete",
            outcome: result.IsSuccess ? "success" : "client_error",
            statusCode,
            clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: $"run:{runId};result:{(result.IsSuccess ? "deleted" : result.ErrorCode)}");

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
                CollectionRunDeleteOutcome deletion when deletion.IsSuccess => new
                {
                    status = "deleted",
                },
                CollectionRunDeleteOutcome deletion => new
                {
                    error = deletion.ErrorCode ?? "collection_run_delete_failed",
                },
                _ => new { error = "collection_run_operation_failed" },
            };

            await Results.Json(payload, statusCode: statusCode)
                .ExecuteAsync(httpContext)
                .ConfigureAwait(false);
        }
    }
}
