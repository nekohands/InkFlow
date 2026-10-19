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

        contentPolicy.MapPost("/takedowns", (
            ContentPolicyTakedownRequest? request,
            ClaimsPrincipal principal,
            ICanonicalBookRepository books,
            IContentPolicyService policy,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) => HandleCommandAsync(
                request?.BookId ?? Guid.Empty,
                request?.Reason,
                principal,
                books,
                policy,
                ContentPolicyAction.Takedown,
                httpContext,
                auditSink,
                clock,
                ct));

        contentPolicy.MapPost("/takedowns/{bookId:guid}/restore", (
            Guid bookId,
            ContentPolicyRestoreRequest? request,
            ClaimsPrincipal principal,
            ICanonicalBookRepository books,
            IContentPolicyService policy,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) => HandleCommandAsync(
                bookId,
                request?.Reason,
                principal,
                books,
                policy,
                ContentPolicyAction.Restore,
                httpContext,
                auditSink,
                clock,
                ct));
    }

    private static async Task<IResult> HandleCommandAsync(
        Guid bookId,
        string? rawReason,
        ClaimsPrincipal principal,
        ICanonicalBookRepository books,
        IContentPolicyService policy,
        ContentPolicyAction action,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
        {
            return Results.Unauthorized();
        }

        if (bookId == Guid.Empty || string.IsNullOrWhiteSpace(rawReason))
        {
            return Results.BadRequest(new { error = "book_id_and_reason_required" });
        }

        if (rawReason.Trim().Length > ContentPolicyDecision.MaxReasonLength)
        {
            return Results.BadRequest(new { error = "reason_too_long" });
        }

        if (await books.GetAsync(bookId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var reason = rawReason.Trim();
        var result = action == ContentPolicyAction.Takedown
            ? await policy.TakedownAsync(bookId, actorId, reason, cancellationToken)
            : await policy.RestoreAsync(bookId, actorId, reason, cancellationToken);
        return Command(
            result,
            action,
            actorId,
            reason,
            httpContext,
            auditSink,
            clock,
            cancellationToken);
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
