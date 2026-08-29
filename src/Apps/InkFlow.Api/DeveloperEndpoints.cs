using System.Diagnostics;
using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using InkFlow.Modules.Developers.Infrastructure.Authentication;
using InkFlow.Modules.Identity.Application;
using Microsoft.Extensions.Logging;

namespace InkFlow.Api;

public static class DeveloperEndpointPolicies
{
    public const string CatalogRead = "developer-catalog-read";
}

internal sealed class DeveloperEndpointLogCategory
{
}

public sealed record DeveloperApplicationRequest(string? Name);

public sealed record DeveloperApiKeyRequest(string? Name, int? ExpiresInDays);

public sealed record DeveloperEntitlementRequest(string? PlanCode, string? Reason);

public sealed record DeveloperApplicationResponse(
    Guid ApplicationId,
    Guid UserId,
    string Name,
    string Environment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);

public sealed record DeveloperApiKeyResponse(
    Guid KeyId,
    Guid ApplicationId,
    string Name,
    string Prefix,
    string Scope,
    string Environment,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

public sealed record DeveloperApiKeyIssueResponse(
    DeveloperApiKeyResponse Key,
    string ApiKey);

public sealed record DeveloperPlanResponse(
    string Code,
    int Version,
    string Name,
    long MonthlyQuotaUnits,
    string QuotaAlgorithmVersion,
    IReadOnlyList<string> Entitlements);

public sealed record DeveloperQuotaResponse(
    string PlanCode,
    int PlanVersion,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    long LimitUnits,
    long UsedUnits,
    long RemainingUnits,
    string AlgorithmVersion);

public sealed record DeveloperEntitlementResponse(
    Guid UserId,
    DeveloperPlanResponse Plan,
    DateTimeOffset EffectiveAt,
    DeveloperQuotaResponse? Quota);

/// <summary>把 Identity 的用户状态作为 Developers 与 Billing 的只读组合根依赖。</summary>
public sealed class DeveloperUserStatusReader(IUserRepository users)
    : IDeveloperUserStatusReader, IBillingUserStatusReader
{
    public async Task<bool> IsActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        return user?.CanAuthenticate == true;
    }
}

public static class DeveloperEndpointMapping
{
    public static void MapDeveloperEndpoints(
        this WebApplication app,
        RouteGroupBuilder api)
    {
        MapManagementEndpoints(api);
        MapCatalogEndpoints(app);
    }

    private static void MapManagementEndpoints(RouteGroupBuilder api)
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
            if (!TryGetUserId(principal, out var userId))
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
            if (!TryGetUserId(principal, out var userId))
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
            if (!TryGetUserId(principal, out var userId))
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
            if (!TryGetUserId(principal, out var userId))
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
            if (!TryGetUserId(principal, out var userId))
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
            if (!TryGetUserId(principal, out var userId))
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
            if (!TryGetUserId(principal, out var userId))
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
            if (!TryGetUserId(principal, out var userId))
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

            return Results.Ok(ToResponse(value, snapshot));
        });

        var commercialAdmin = api.MapGroup("/admin")
            .RequireAuthorization(IdentityPolicies.CommercialManagement);
        commercialAdmin.MapGet("/plans", async (
            IEntitlementService entitlements,
            CancellationToken ct) =>
        {
            var plans = await entitlements.ListPlansAsync(ct).ConfigureAwait(false);
            return Results.Ok(plans.Select(ToResponse));
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
            if (!TryGetUserId(principal, out var actorId))
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
                ? Results.Ok(ToResponse(result.Value, null))
                : Results.Json(
                    new { error = EntitlementError(result.Status) },
                    statusCode: statusCode);
        });
    }

    private static void MapCatalogEndpoints(WebApplication app)
    {
        var developer = app.MapGroup("/api/developer/v1")
            .RequireRateLimiting(ApiRateLimitPolicies.DeveloperPolicyName)
            .RequireAuthorization(DeveloperEndpointPolicies.CatalogRead);

        developer.MapGet("/search", async (
            string? q,
            int? limit,
            ClaimsPrincipal principal,
            CatalogQueryService catalog,
            IQuotaService quota,
            TimeProvider clock,
            ILogger<DeveloperEndpointLogCategory> logger,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (!TryGetDeveloperIdentity(principal, out var identity))
            {
                return (IResult)Results.Unauthorized();
            }

            if (!TryGetLimit(limit, out var boundedLimit) || q?.Length > 256)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }

            var quotaResult = await DeveloperEndpointResults
                .ReserveAsync(
                    quota,
                    clock,
                    logger,
                    httpContext,
                    identity,
                    "catalog.search",
                    1,
                    ct)
                .ConfigureAwait(false);
            if (quotaResult is not null)
            {
                return quotaResult;
            }

            var books = await catalog.SearchBooksAsync(q ?? string.Empty, ct).ConfigureAwait(false);
            DeveloperEndpointResults.NoStore(httpContext);
            return Results.Ok(books.Take(boundedLimit).Select(ToResponse));
        });

        developer.MapGet("/books", async (
            int? limit,
            ClaimsPrincipal principal,
            CatalogQueryService catalog,
            IQuotaService quota,
            TimeProvider clock,
            ILogger<DeveloperEndpointLogCategory> logger,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (!TryGetDeveloperIdentity(principal, out var identity))
            {
                return (IResult)Results.Unauthorized();
            }

            if (!TryGetLimit(limit, out var boundedLimit))
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }

            var quotaResult = await DeveloperEndpointResults
                .ReserveAsync(
                    quota,
                    clock,
                    logger,
                    httpContext,
                    identity,
                    "catalog.list_books",
                    1,
                    ct)
                .ConfigureAwait(false);
            if (quotaResult is not null)
            {
                return quotaResult;
            }

            var books = await catalog.ListBooksAsync(ct).ConfigureAwait(false);
            DeveloperEndpointResults.NoStore(httpContext);
            return Results.Ok(books.Take(boundedLimit).Select(ToResponse));
        });

        developer.MapGet("/books/{bookId:guid}", async (
            Guid bookId,
            ClaimsPrincipal principal,
            CatalogQueryService catalog,
            IQuotaService quota,
            TimeProvider clock,
            ILogger<DeveloperEndpointLogCategory> logger,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (!TryGetDeveloperIdentity(principal, out var identity))
            {
                return (IResult)Results.Unauthorized();
            }

            var quotaResult = await DeveloperEndpointResults
                .ReserveAsync(
                    quota,
                    clock,
                    logger,
                    httpContext,
                    identity,
                    "catalog.get_book",
                    1,
                    ct)
                .ConfigureAwait(false);
            if (quotaResult is not null)
            {
                return quotaResult;
            }

            var book = await catalog.GetBookAsync(bookId, ct).ConfigureAwait(false);
            if (book is null)
            {
                return Results.NotFound(new { error = "book_not_found" });
            }

            DeveloperEndpointResults.NoStore(httpContext);
            return Results.Ok(ToResponse(book));
        });

        developer.MapGet("/books/{bookId:guid}/chapters", async (
            Guid bookId,
            ClaimsPrincipal principal,
            CatalogQueryService catalog,
            IQuotaService quota,
            TimeProvider clock,
            ILogger<DeveloperEndpointLogCategory> logger,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (!TryGetDeveloperIdentity(principal, out var identity))
            {
                return (IResult)Results.Unauthorized();
            }

            var quotaResult = await DeveloperEndpointResults
                .ReserveAsync(
                    quota,
                    clock,
                    logger,
                    httpContext,
                    identity,
                    "catalog.list_chapters",
                    1,
                    ct)
                .ConfigureAwait(false);
            if (quotaResult is not null)
            {
                return quotaResult;
            }

            var book = await catalog.GetBookAsync(bookId, ct).ConfigureAwait(false);
            if (book is null)
            {
                return Results.NotFound(new { error = "book_not_found" });
            }

            DeveloperEndpointResults.NoStore(httpContext);
            return Results.Ok(book.Chapters.Select(c => new DeveloperChapterSummary(
                c.ChapterId,
                c.Index,
                c.Title)));
        });

        developer.MapGet("/chapters/{chapterId:guid}/content", async (
            Guid chapterId,
            ClaimsPrincipal principal,
            CatalogQueryService catalog,
            IQuotaService quota,
            TimeProvider clock,
            ILogger<DeveloperEndpointLogCategory> logger,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (!TryGetDeveloperIdentity(principal, out var identity))
            {
                return (IResult)Results.Unauthorized();
            }

            var quotaResult = await DeveloperEndpointResults
                .ReserveAsync(
                    quota,
                    clock,
                    logger,
                    httpContext,
                    identity,
                    "catalog.get_content",
                    5,
                    ct)
                .ConfigureAwait(false);
            if (quotaResult is not null)
            {
                return quotaResult;
            }

            var content = await catalog.GetChapterContentAsync(chapterId, ct).ConfigureAwait(false);
            if (content is null)
            {
                return Results.NotFound(new { error = "chapter_not_found" });
            }

            DeveloperEndpointResults.NoStore(httpContext);
            return Results.Ok(new DeveloperChapterContent(
                content.ChapterId,
                content.BookId,
                content.Index,
                content.Title,
                content.Paragraphs));
        });
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out userId) && userId != Guid.Empty;

    private static bool TryGetDeveloperIdentity(
        ClaimsPrincipal principal,
        out DeveloperIdentity identity)
    {
        identity = default;
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId) || userId == Guid.Empty ||
            !Guid.TryParse(principal.FindFirstValue(DeveloperAuthenticationDefaults.ApplicationIdClaim), out var applicationId) ||
            applicationId == Guid.Empty ||
            !Guid.TryParse(principal.FindFirstValue(DeveloperAuthenticationDefaults.ApiKeyIdClaim), out var apiKeyId) ||
            apiKeyId == Guid.Empty)
        {
            return false;
        }

        identity = new DeveloperIdentity(userId, applicationId, apiKeyId);
        return true;
    }

    private static bool TryGetLimit(int? limit, out int bounded)
    {
        bounded = limit ?? 20;
        return bounded is >= 1 and <= 100;
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

    private static int EntitlementStatusCode(EntitlementOperationStatus status) => status switch
    {
        EntitlementOperationStatus.Success => StatusCodes.Status200OK,
        EntitlementOperationStatus.PlanNotFound => StatusCodes.Status404NotFound,
        EntitlementOperationStatus.UserNotFound => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string EntitlementError(EntitlementOperationStatus status) => status switch
    {
        EntitlementOperationStatus.PlanNotFound => "plan_not_found",
        EntitlementOperationStatus.UserNotFound => "user_not_found",
        _ => "invalid_request",
    };

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

    private static DeveloperPlanResponse ToResponse(PlanView value) =>
        new(
            value.Code,
            value.Version,
            value.Name,
            value.MonthlyQuotaUnits,
            value.QuotaAlgorithmVersion,
            value.Entitlements);

    private static DeveloperEntitlementResponse ToResponse(
        EntitlementView value,
        QuotaSnapshot? snapshot) =>
        new(
            value.UserId,
            ToResponse(value.Plan),
            value.EffectiveAt,
            snapshot is null
                ? null
                : new DeveloperQuotaResponse(
                    snapshot.PlanCode,
                    snapshot.PlanVersion,
                    snapshot.PeriodStart,
                    snapshot.PeriodEnd,
                    snapshot.LimitUnits,
                    snapshot.UsedUnits,
                    snapshot.RemainingUnits,
                    snapshot.AlgorithmVersion));

    private static DeveloperBookSummary ToResponse(BookListItem value) =>
        new(value.Id, value.Title, value.Author, value.ChapterCount);

    private static DeveloperBookDetail ToResponse(BookDetail value) =>
        new(value.Id, value.Title, value.Author, value.Chapters.Count);

    internal readonly record struct DeveloperIdentity(
        Guid UserId,
        Guid ApplicationId,
        Guid ApiKeyId);
}

internal static class DeveloperEndpointResults
{
    public static async Task<IResult?> ReserveAsync(
        IQuotaService quota,
        TimeProvider clock,
        ILogger logger,
        HttpContext httpContext,
        DeveloperEndpointMapping.DeveloperIdentity identity,
        string operation,
        long units,
        CancellationToken cancellationToken)
    {
        QuotaReservationResult result;
        try
        {
            result = await quota
                .ReserveAsync(
                    new QuotaReservationRequest(
                        identity.UserId,
                        identity.ApplicationId,
                        identity.ApiKeyId,
                        operation,
                        units,
                        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "developer API quota reservation failed for {Operation}", operation);
            NoStore(httpContext);
            return Results.Json(
                new { error = "quota_unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        switch (result.Status)
        {
            case QuotaReservationStatus.Reserved:
                return null;
            case QuotaReservationStatus.Forbidden:
                NoStore(httpContext);
                return Results.Json(
                    new { error = "developer_api_forbidden" },
                    statusCode: StatusCodes.Status403Forbidden);
            case QuotaReservationStatus.InvalidRequest:
                NoStore(httpContext);
                return Results.BadRequest(new { error = "invalid_request" });
            case QuotaReservationStatus.Exceeded:
            {
                var now = clock.GetUtcNow();
                var periodEnd = result.Snapshot?.PeriodEnd ?? now.AddMinutes(1);
                var retryAfter = Math.Max(1, (int)Math.Ceiling((periodEnd - now).TotalSeconds));
                httpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
                NoStore(httpContext);
                return Results.Json(
                    new
                    {
                        error = "quota_exceeded",
                        periodEnd,
                        remainingUnits = result.Snapshot?.RemainingUnits ?? 0,
                    },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
            default:
                NoStore(httpContext);
                return Results.Json(
                    new { error = "quota_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "private, no-store";
    }
}

internal static class DeveloperEndpointAudit
{
    public static async Task RecordAsync(
        IAuditEventSink auditSink,
        TimeProvider clock,
        HttpContext httpContext,
        string action,
        string resource,
        Guid actorId,
        int statusCode,
        string outcome,
        string reference,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        var auditEvent = AuditEvent.Create(
            action,
            resource,
            outcome,
            statusCode,
            clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId.ToString("D"),
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: reference);
        try
        {
            await auditSink.AppendAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 审计故障不回滚已经完成的密钥/套餐命令；主请求审计中间件仍会保留结果。
            var logger = httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("InkFlow.Api.DeveloperEndpointAudit");
            logger.LogError(exception, "developer command audit failed for {AuditEventId}", auditEvent.Id);
        }
    }
}
