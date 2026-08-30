using System.Diagnostics;
using System.Security.Claims;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Api;

public sealed record CreateBookPackageRequest(string? Format);

/// <summary>书籍包创建、状态查询和下载 API；仅运维角色可访问。</summary>
public static class BookPackageEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var read = api.MapGroup("/admin/packages")
            .RequireAuthorization(IdentityPolicies.OperationsRead);
        read.MapGet("/{packageId:guid}", async (
            Guid packageId,
            BookPackageService packages,
            CancellationToken ct) =>
        {
            var value = await packages.GetViewAsync(packageId, ct).ConfigureAwait(false);
            return value is null
                ? Results.NotFound(new { error = "package_not_found" })
                : Results.Ok(ToResponse(value));
        });
        read.MapGet("/{packageId:guid}/download", async (
            Guid packageId,
            BookPackageService packages,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
            {
                return (IResult)Results.Unauthorized();
            }

            var value = await packages.GetViewAsync(packageId, ct).ConfigureAwait(false);
            if (value is null)
            {
                await AuditDownloadAsync(
                        packageId,
                        actorId,
                        httpContext,
                        auditSink,
                        clock,
                        StatusCodes.Status404NotFound,
                        "package_not_found",
                        ct)
                    .ConfigureAwait(false);
                return (IResult)Results.NotFound(new { error = "package_not_found" });
            }

            if (value.Status != BookPackageJobStatus.Completed ||
                string.IsNullOrWhiteSpace(value.ArtifactFileName))
            {
                await AuditDownloadAsync(
                        packageId,
                        actorId,
                        httpContext,
                        auditSink,
                        clock,
                        StatusCodes.Status409Conflict,
                        $"package_not_ready:{value.Status}",
                        ct)
                    .ConfigureAwait(false);
                return Results.Conflict(new
                {
                    error = "package_not_ready",
                    status = value.Status.ToString().ToLowerInvariant(),
                });
            }

            var stream = await packages.OpenCompletedAsync(packageId, ct).ConfigureAwait(false);
            if (stream is null)
            {
                await AuditDownloadAsync(
                        packageId,
                        actorId,
                        httpContext,
                        auditSink,
                        clock,
                        StatusCodes.Status404NotFound,
                        "package_artifact_not_found",
                        ct)
                    .ConfigureAwait(false);
                return Results.NotFound(new { error = "package_artifact_not_found" });
            }

            await AuditDownloadAsync(
                    packageId,
                    actorId,
                    httpContext,
                    auditSink,
                    clock,
                    StatusCodes.Status200OK,
                    value.ArtifactFileName,
                    ct)
                .ConfigureAwait(false);
            return Results.File(
                    stream,
                    ContentTypeFor(value.Format),
                    value.ArtifactFileName,
                    enableRangeProcessing: true);
        });

        var write = api.MapGroup("/admin/books")
            .RequireAuthorization(IdentityPolicies.CrawlerRepair);
        write.MapPost("/{bookId:guid}/packages", async (
            Guid bookId,
            CreateBookPackageRequest? request,
            ClaimsPrincipal principal,
            BookPackageService packages,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
            {
                return (IResult)Results.Unauthorized();
            }

            if (!Enum.TryParse<BookPackageFormat>(request?.Format, ignoreCase: true, out var format) ||
                !Enum.IsDefined(format))
            {
                return (IResult)Results.BadRequest(new { error = "invalid_package_format" });
            }

            var result = await packages.CreateAsync(bookId, format, ct).ConfigureAwait(false);
            var statusCode = result.IsSuccess
                ? StatusCodes.Status202Accepted
                : result.ErrorCode == "package.book-not-found"
                    ? StatusCodes.Status404NotFound
                    : result.ErrorCode == "package.invalid-request"
                        ? StatusCodes.Status400BadRequest
                        : StatusCodes.Status409Conflict;
            var audit = AuditEvent.Create(
                action: "book.package.create",
                resource: $"/api/v1/admin/books/{bookId}/packages",
                outcome: result.IsSuccess ? "success" : "client_error",
                statusCode,
                clock.GetUtcNow(),
                actorType: "authenticated",
                actorId: actorId,
                traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
                reference: result.Package is { } package
                    ? $"package:{package.Id};book:{bookId};format:{format}"
                    : $"book:{bookId};format:{format};error:{result.ErrorCode}");
            await auditSink.AppendAsync(audit, ct).ConfigureAwait(false);

            return result.IsSuccess
                ? Results.Json(new { status = "accepted", package = ToResponse(result.Package!) }, statusCode: statusCode)
                : Results.Json(new { error = result.ErrorCode ?? "package_create_failed" }, statusCode: statusCode);
        });
    }

    public static object ToResponse(BookPackageView value) => new
    {
        id = value.Id,
        canonicalBookId = value.CanonicalBookId,
        format = value.Format.ToString().ToLowerInvariant(),
        status = value.Status.ToString().ToLowerInvariant(),
        totalChapterCount = value.TotalChapterCount,
        completedChapterCount = value.CompletedChapterCount,
        progressPercent = value.ProgressPercent,
        artifactFileName = value.ArtifactFileName,
        artifactSha256 = value.ArtifactSha256,
        artifactLength = value.ArtifactLength,
        failureReason = value.FailureReason,
        createdAt = value.CreatedAt,
        updatedAt = value.UpdatedAt,
        expiresAt = value.ExpiresAt,
    };

    private static ValueTask AuditDownloadAsync(
        Guid packageId,
        string actorId,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        int statusCode,
        string reference,
        CancellationToken cancellationToken) =>
        auditSink.AppendAsync(
            AuditEvent.Create(
                action: "book.package.download",
                resource: $"/api/v1/admin/packages/{packageId}/download",
                outcome: statusCode == StatusCodes.Status200OK ? "success" : "client_error",
                statusCode,
                clock.GetUtcNow(),
                actorType: "authenticated",
                actorId: actorId,
                traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
                reference: $"package:{packageId};{reference}"),
            cancellationToken);

    private static string ContentTypeFor(BookPackageFormat format) => format switch
    {
        BookPackageFormat.Zip => "application/zip",
        BookPackageFormat.Epub => "application/epub+zip",
        BookPackageFormat.Txt => "text/plain; charset=utf-8",
        _ => "application/octet-stream",
    };
}
