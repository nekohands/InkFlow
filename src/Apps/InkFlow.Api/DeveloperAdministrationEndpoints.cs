using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Api;

public static class DeveloperAdministrationEndpoints
{
    public static void MapDeveloperAdministrationEndpoints(this RouteGroupBuilder api)
    {
        var commercialAdmin = api.MapGroup("/admin")
            .RequireAuthorization(IdentityPolicies.CommercialManagement);
        commercialAdmin.MapGet("/plans", async (
            IEntitlementService entitlements,
            CancellationToken ct) =>
        {
            var plans = await entitlements.ListPlansAsync(ct).ConfigureAwait(false);
            return Results.Ok(plans.Select(DeveloperEntitlementResponses.ToResponse));
        });

        commercialAdmin.MapPut("/users/{userId:guid}/entitlement", async (
            Guid userId,
            DeveloperEntitlementRequest? request,
            ClaimsPrincipal principal,
            IEntitlementService entitlements,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var actorId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await entitlements
                .AssignAsync(actorId, userId, request?.PlanCode, request?.Reason, ct)
                .ConfigureAwait(false);
            var statusCode = EntitlementStatusCode(result.Status);
            await DeveloperEndpointAudit.RecordAsync(
                    auditSink,
                    clock,
                    httpContext,
                    "billing.entitlement.assign",
                    $"/api/v1/admin/users/{userId:D}/entitlement",
                    actorId,
                    statusCode,
                    result.Status == EntitlementOperationStatus.Success ? "success" : "client_error",
                    $"user:{userId:D};plan:{request?.PlanCode?.Trim() ?? ""};status:{result.Status}",
                    ct,
                    request?.Reason)
                .ConfigureAwait(false);

            return result.Status == EntitlementOperationStatus.Success && result.Value is not null
                ? Results.Ok(DeveloperEntitlementResponses.ToResponse(result.Value, null))
                : Results.Json(
                    new { error = EntitlementError(result.Status) },
                    statusCode: statusCode);
        });
    }

    private static int EntitlementStatusCode(EntitlementOperationStatus status) => status switch
    {
        EntitlementOperationStatus.Success => StatusCodes.Status200OK,
        EntitlementOperationStatus.PlanNotFound => StatusCodes.Status404NotFound,
        EntitlementOperationStatus.UserNotFound => StatusCodes.Status404NotFound,
        EntitlementOperationStatus.ActorNotAllowed => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string EntitlementError(EntitlementOperationStatus status) => status switch
    {
        EntitlementOperationStatus.PlanNotFound => "plan_not_found",
        EntitlementOperationStatus.UserNotFound => "user_not_found",
        EntitlementOperationStatus.ActorNotAllowed => "entitlement_management_forbidden",
        _ => "invalid_request",
    };
}
