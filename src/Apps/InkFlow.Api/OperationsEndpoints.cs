using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Operations.Application;

namespace InkFlow.Api;

public static class OperationsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var operationsRead = api.MapGroup("/admin")
            .RequireAuthorization(IdentityPolicies.OperationsRead);

        var auditRead = api.MapGroup("/admin")
            .RequireAuthorization(IdentityPolicies.AuditRead);

        auditRead.MapGet("/audit/events", async (
            string? from,
            string? to,
            string? action,
            string? outcome,
            string? actorId,
            string? cursor,
            int? limit,
            IAuditEventReader auditReader,
            CancellationToken ct) =>
        {
            if (!AuditEndpointResults.TryCreateQuery(
                    from,
                    to,
                    action,
                    outcome,
                    actorId,
                    cursor,
                    limit,
                    out var query,
                    out var error))
            {
                return (IResult)Results.BadRequest(new { error });
            }

            try
            {
                var page = await auditReader.QueryAsync(query!, ct).ConfigureAwait(false);
                return Results.Ok(AuditEndpointResults.ToResponse(page));
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return Results.Json(
                    new { error = "audit_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        operationsRead.MapGet("/crawler/dead-letters", async (
            int? limit,
            ICrawlerTaskRepository tasks,
            CancellationToken ct) =>
        {
            var boundedLimit = Math.Clamp(limit ?? 50, 1, 100);
            var deadLetters = await tasks.ListDeadLettersAsync(boundedLimit, ct);
            return Results.Ok(deadLetters);
        });

        operationsRead.MapGet("/consistency", async (
            IConsistencyCheckService consistency,
            CancellationToken ct) =>
        {
            var report = await consistency.CheckAsync(ct);
            return Results.Ok(report);
        });

        var operationsSnapshotRead = api.MapGroup("/admin/operations")
            .RequireAuthorization(IdentityPolicies.OperationsSnapshotRead);

        operationsSnapshotRead.MapGet("/overview", async (
            int? limit,
            ClaimsPrincipal principal,
            IOperationsCenterReader operations,
            IResourceAuthorizationService authorization,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetIdentity(
                    principal,
                    out var userId,
                    out var role))
            {
                return (IResult)Results.Unauthorized();
            }

            if (role == UserRole.Reader)
            {
                return Results.Ok(await operations.ReadSourcesOnlyAsync(ct).ConfigureAwait(false));
            }

            var boundedLimit = limit ?? OperationsCenterReader.DefaultLimit;
            var snapshot = role == UserRole.Administrator
                ? await operations.ReadAsync(boundedLimit, ct).ConfigureAwait(false)
                : await operations.ReadForSourcesAsync(
                        boundedLimit,
                        await authorization.ListAllowedResourceIdsAsync(
                            userId,
                            role,
                            IdentityPermissions.SourceRead,
                            IdentityResourceTypes.Source,
                            ct).ConfigureAwait(false),
                        ct)
                    .ConfigureAwait(false);
            return Results.Ok(snapshot);
        });

        operationsRead.MapGet("/operations/alerts", async (
            int? limit,
            ClaimsPrincipal principal,
            IOperationsAlertReader alerts,
            IResourceAuthorizationService authorization,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetIdentity(
                    principal,
                    out var userId,
                    out var role))
            {
                return (IResult)Results.Unauthorized();
            }

            var boundedLimit = limit ?? OperationsAlertReader.DefaultLimit;
            var snapshot = role == UserRole.Administrator
                ? await alerts.ReadAsync(boundedLimit, ct).ConfigureAwait(false)
                : await alerts.ReadForSourcesAsync(
                        boundedLimit,
                        await authorization.ListAllowedResourceIdsAsync(
                            userId,
                            role,
                            IdentityPermissions.SourceRead,
                            IdentityResourceTypes.Source,
                            ct).ConfigureAwait(false),
                        ct)
                    .ConfigureAwait(false);
            return Results.Ok(snapshot);
        });

        operationsRead.MapGet("/operations/alerts/history", async (
            int? limit,
            string? cursor,
            ClaimsPrincipal principal,
            IOperationsAlertHistoryReader history,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetIdentity(
                    principal,
                    out _,
                    out var role))
            {
                return (IResult)Results.Unauthorized();
            }

            // History is a platform-wide incident view. Operators receive source-scoped
            // snapshots, so only administrators may query this unfiltered history.
            if (role != UserRole.Administrator)
            {
                return (IResult)Results.Forbid();
            }

            if (!OperationsAlertHistoryEndpointResults.TryCreateQuery(
                    limit,
                    cursor,
                    out var boundedLimit,
                    out var before,
                    out var error))
            {
                return (IResult)Results.BadRequest(new { error });
            }

            try
            {
                var page = await history.QueryAsync(boundedLimit, before, ct).ConfigureAwait(false);
                return Results.Ok(OperationsAlertHistoryEndpointResults.ToResponse(page));
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return Results.Json(
                    new { error = "operations_alert_history_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        var repair = api.MapGroup("/admin")
            .RequireAuthorization(IdentityPolicies.CrawlerRepair);

        CollectionRunEndpoints.Map(api);
        SourceLifecycleEndpointResults.Map(api);
        BookPackageEndpoints.Map(api);

        repair.MapPost("/crawler/dead-letters/{deadLetterId:guid}/replay", async (
            Guid deadLetterId,
            ReplayDeadLetterRequest request,
            ClaimsPrincipal principal,
            ICrawlerTaskRepairRepository repairRepository,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
            {
                return (IResult)Results.Unauthorized();
            }

            DeadLetterReplayCommand command;
            try
            {
                command = DeadLetterReplayCommand.Create(
                    deadLetterId,
                    actorId,
                    request.Reason ?? string.Empty);
            }
            catch (ArgumentException)
            {
                return (IResult)Results.BadRequest(new { error = "invalid_replay_request" });
            }

            var result = await repairRepository.ReplayDeadLetterAsync(command, clock.GetUtcNow(), ct);
            return RepairEndpointResults.Replay(
                result,
                deadLetterId,
                actorId,
                command.ReplayReason,
                httpContext,
                auditSink,
                clock,
                ct);
        });
    }
}
