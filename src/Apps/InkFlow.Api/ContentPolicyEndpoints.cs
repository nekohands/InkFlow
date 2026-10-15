using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Identity.Application;
using System.Security.Claims;
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
    public static void Map(RouteGroupBuilder api)
    {
        var contentPolicy = api.MapGroup("/admin/content")
            .RequireAuthorization(IdentityPolicies.ContentModeration);

        contentPolicy.MapGet("/takedowns", async (
            int? limit,
            IContentPolicyService policy,
            CancellationToken ct) =>
        {
            var boundedLimit = Math.Clamp(limit ?? 50, 1, ContentPolicyService.MaxListLimit);
            var statuses = await policy.ListAsync(
                takenDownOnly: true,
                limit: boundedLimit,
                cancellationToken: ct);
            return Results.Ok(statuses);
        });

        contentPolicy.MapPost("/takedowns", async (
            ContentPolicyTakedownRequest? request,
            ClaimsPrincipal principal,
            ICanonicalBookRepository books,
            IContentPolicyService policy,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
            {
                return (IResult)Results.Unauthorized();
            }

            if (request is null || request.BookId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "book_id_and_reason_required" });
            }

            if (request.Reason.Trim().Length > ContentPolicyDecision.MaxReasonLength)
            {
                return Results.BadRequest(new { error = "reason_too_long" });
            }

            if (await books.GetAsync(request.BookId, ct) is null)
            {
                return Results.NotFound();
            }

            var reason = request.Reason.Trim();
            var result = await policy.TakedownAsync(request.BookId, actorId, reason, ct);
            return ContentPolicyEndpointResults.Command(
                result,
                ContentPolicyAction.Takedown,
                actorId,
                reason,
                httpContext,
                auditSink,
                clock,
                ct);
        });

        contentPolicy.MapPost("/takedowns/{bookId:guid}/restore", async (
            Guid bookId,
            ContentPolicyRestoreRequest? request,
            ClaimsPrincipal principal,
            ICanonicalBookRepository books,
            IContentPolicyService policy,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
            {
                return (IResult)Results.Unauthorized();
            }

            if (request is null || bookId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "book_id_and_reason_required" });
            }

            if (request.Reason.Trim().Length > ContentPolicyDecision.MaxReasonLength)
            {
                return Results.BadRequest(new { error = "reason_too_long" });
            }

            if (await books.GetAsync(bookId, ct) is null)
            {
                return Results.NotFound();
            }

            var reason = request.Reason.Trim();
            var result = await policy.RestoreAsync(bookId, actorId, reason, ct);
            return ContentPolicyEndpointResults.Command(
                result,
                ContentPolicyAction.Restore,
                actorId,
                reason,
                httpContext,
                auditSink,
                clock,
                ct);
        });
    }

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
            auditEvent,
            auditSink,
            cancellationToken,
            () => CreateResponse(result, action));
    }

    private static IResult CreateResponse(ContentPolicyCommandResult result, ContentPolicyAction action)
    {
        return Results.Json(new
        {
            status = result.Changed ? "applied" : "already_in_state",
            action = action.ToString().ToLowerInvariant(),
            bookId = result.CanonicalBookId,
            isTakedown = result.IsTakedown,
            decisionId = result.Decision?.Id,
            decidedAt = result.Decision?.CreatedAt,
        });
    }
}
