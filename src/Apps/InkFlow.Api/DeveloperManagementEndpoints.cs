using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Developers.Application;

namespace InkFlow.Api;

public static class DeveloperManagementEndpoints
{
    public static void MapDeveloperManagementEndpoints(this RouteGroupBuilder api)
    {
        var applications = api.MapGroup("/me/developer-applications")
            .RequireAuthorization();

        applications.MapPost("/", async (
            DeveloperApplicationRequest? request,
            ClaimsPrincipal principal,
            IDeveloperApplicationService service,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await service
                .CreateApplicationAsync(userId, request?.Name, ct)
                .ConfigureAwait(false);
            var statusCode = DeveloperOperationStatusCode(result.Status);
            await DeveloperEndpointAudit.RecordAsync(
                    auditSink,
                    clock,
                    httpContext,
                    "developer.application.create",
                    "/api/v1/me/developer-applications",
                    userId,
                    statusCode,
                    result.Status == DeveloperOperationStatus.Success ? "success" : "client_error",
                    result.Value is null ? $"status:{result.Status}" : $"application:{result.Value.ApplicationId}",
                    ct)
                .ConfigureAwait(false);

            return result.IsSuccess && result.Value is not null
                ? Results.Created(
                    $"/api/v1/me/developer-applications/{result.Value.ApplicationId:D}",
                    ToResponse(result.Value))
                : Error(result.Status);
        });

        applications.MapGet("/", async (
            ClaimsPrincipal principal,
            IDeveloperApplicationService service,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var values = await service.ListApplicationsAsync(userId, ct).ConfigureAwait(false);
            return Results.Ok(values.Select(ToResponse));
        });

        applications.MapDelete("/{applicationId:guid}", async (
            Guid applicationId,
            ClaimsPrincipal principal,
            IDeveloperApplicationService service,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var status = await service
                .RevokeApplicationAsync(userId, applicationId, ct)
                .ConfigureAwait(false);
            var statusCode = DeveloperOperationStatusCode(status);
            await DeveloperEndpointAudit.RecordAsync(
                    auditSink,
                    clock,
                    httpContext,
                    "developer.application.revoke",
                    $"/api/v1/me/developer-applications/{applicationId:D}",
                    userId,
                    statusCode,
                    status == DeveloperOperationStatus.Success ? "success" : "client_error",
                    $"application:{applicationId:D};status:{status}",
                    ct)
                .ConfigureAwait(false);

            return status == DeveloperOperationStatus.Success
                ? Results.NoContent()
                : Error(status);
        });

        applications.MapPost("/{applicationId:guid}/keys", async (
            Guid applicationId,
            DeveloperApiKeyRequest? request,
            ClaimsPrincipal principal,
            IDeveloperApplicationService service,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await service
                .IssueKeyAsync(
                    userId,
                    applicationId,
                    request?.Name,
                    request?.ExpiresInDays,
                    ct)
                .ConfigureAwait(false);
            var statusCode = DeveloperOperationStatusCode(result.Status);
            await DeveloperEndpointAudit.RecordAsync(
                    auditSink,
                    clock,
                    httpContext,
                    "developer.api_key.create",
                    $"/api/v1/me/developer-applications/{applicationId:D}/keys",
                    userId,
                    statusCode,
                    result.Status == DeveloperOperationStatus.Success ? "success" : "client_error",
                    result.Value is null
                        ? $"application:{applicationId:D};status:{result.Status}"
                        : $"application:{applicationId:D};key:{result.Value.Key.KeyId:D}",
                    ct)
                .ConfigureAwait(false);

            return result.IsSuccess && result.Value is not null
                ? Results.Created(
                    $"/api/v1/me/developer-applications/{applicationId:D}/keys/{result.Value.Key.KeyId:D}",
                    ToIssueResponse(result.Value))
                : Error(result.Status);
        });

        applications.MapGet("/{applicationId:guid}/keys", async (
            Guid applicationId,
            ClaimsPrincipal principal,
            IDeveloperApplicationService service,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var values = await service.ListKeysAsync(userId, applicationId, ct).ConfigureAwait(false);
            return Results.Ok(values.Select(ToResponse));
        });

        applications.MapPost("/{applicationId:guid}/keys/{keyId:guid}/rotate", async (
            Guid applicationId,
            Guid keyId,
            DeveloperApiKeyRequest? request,
            ClaimsPrincipal principal,
            IDeveloperApplicationService service,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await service
                .RotateKeyAsync(userId, applicationId, keyId, request?.ExpiresInDays, ct)
                .ConfigureAwait(false);
            var statusCode = DeveloperOperationStatusCode(result.Status);
            await DeveloperEndpointAudit.RecordAsync(
                    auditSink,
                    clock,
                    httpContext,
                    "developer.api_key.rotate",
                    $"/api/v1/me/developer-applications/{applicationId:D}/keys/{keyId:D}/rotate",
                    userId,
                    statusCode,
                    result.Status == DeveloperOperationStatus.Success ? "success" : "client_error",
                    result.Value is null
                        ? $"application:{applicationId:D};key:{keyId:D};status:{result.Status}"
                        : $"application:{applicationId:D};old-key:{keyId:D};new-key:{result.Value.Key.KeyId:D}",
                    ct)
                .ConfigureAwait(false);

            return result.IsSuccess && result.Value is not null
                ? Results.Ok(ToIssueResponse(result.Value))
                : Error(result.Status);
        });

        applications.MapDelete("/{applicationId:guid}/keys/{keyId:guid}", async (
            Guid applicationId,
            Guid keyId,
            ClaimsPrincipal principal,
            IDeveloperApplicationService service,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var status = await service
                .RevokeKeyAsync(userId, applicationId, keyId, ct)
                .ConfigureAwait(false);
            var statusCode = DeveloperOperationStatusCode(status);
            await DeveloperEndpointAudit.RecordAsync(
                    auditSink,
                    clock,
                    httpContext,
                    "developer.api_key.revoke",
                    $"/api/v1/me/developer-applications/{applicationId:D}/keys/{keyId:D}",
                    userId,
                    statusCode,
                    status == DeveloperOperationStatus.Success ? "success" : "client_error",
                    $"application:{applicationId:D};key:{keyId:D};status:{status}",
                    ct)
                .ConfigureAwait(false);

            return status == DeveloperOperationStatus.Success
                ? Results.NoContent()
                : Error(status);
        });

        var entitlement = api.MapGroup("/me/entitlement")
            .RequireAuthorization();
        entitlement.MapGet("", async (
            ClaimsPrincipal principal,
            IEntitlementService entitlements,
            IQuotaService quota,
            CancellationToken ct) =>
        {
            if (!ApiIdentity.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var value = await entitlements.GetForUserAsync(userId, ct).ConfigureAwait(false);
            if (value is null)
            {
                return Results.NotFound(new { error = "entitlement_not_found" });
            }

            QuotaSnapshot? snapshot;
            try
            {
                snapshot = await quota.GetSnapshotAsync(userId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return Results.Json(
                    new { error = "quota_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(DeveloperEntitlementResponses.ToResponse(value, snapshot));
        });
    }

    private static int DeveloperOperationStatusCode(DeveloperOperationStatus status) => status switch
    {
        DeveloperOperationStatus.Success => StatusCodes.Status200OK,
        DeveloperOperationStatus.NotFound => StatusCodes.Status404NotFound,
        DeveloperOperationStatus.LimitReached => StatusCodes.Status409Conflict,
        DeveloperOperationStatus.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string DeveloperError(DeveloperOperationStatus status) => status switch
    {
        DeveloperOperationStatus.NotFound => "developer_resource_not_found",
        DeveloperOperationStatus.LimitReached => "developer_limit_reached",
        DeveloperOperationStatus.Conflict => "developer_operation_conflict",
        _ => "invalid_request",
    };

    private static IResult Error(DeveloperOperationStatus status) =>
        Results.Json(
            new { error = DeveloperError(status) },
            statusCode: DeveloperOperationStatusCode(status));

    private static DeveloperApplicationResponse ToResponse(DeveloperApplicationView value) =>
        new(
            value.ApplicationId,
            value.UserId,
            value.Name,
            value.Environment.ToString().ToLowerInvariant(),
            value.CreatedAt,
            value.RevokedAt);

    private static DeveloperApiKeyResponse ToResponse(DeveloperApiKeyView value) =>
        new(
            value.KeyId,
            value.ApplicationId,
            value.Name,
            value.Prefix,
            value.Scope,
            value.Environment.ToString().ToLowerInvariant(),
            value.CreatedAt,
            value.ExpiresAt,
            value.LastUsedAt,
            value.RevokedAt);

    private static DeveloperApiKeyIssueResponse ToIssueResponse(IssuedDeveloperApiKey value) =>
        new(ToResponse(value.Key), value.RawKey);
}
