using System.Security.Claims;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Infrastructure.Authentication;

namespace InkFlow.Api;

public static class DeveloperEndpointPolicies
{
    public const string CatalogRead = "developer-catalog-read";
}

public static class DeveloperCatalogEndpoints
{
    public static void MapDeveloperCatalogEndpoints(this WebApplication app)
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

            var books = await catalog
                .SearchBooksAsync(q ?? string.Empty, boundedLimit, ct)
                .ConfigureAwait(false);
            DeveloperEndpointResults.NoStore(httpContext);
            return Results.Ok(books.Select(ToResponse));
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

            var books = await catalog.ListBooksAsync(boundedLimit, ct).ConfigureAwait(false);
            DeveloperEndpointResults.NoStore(httpContext);
            return Results.Ok(books.Select(ToResponse));
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

    private static bool TryGetDeveloperIdentity(
        ClaimsPrincipal principal,
        out DeveloperIdentity identity)
    {
        identity = default;
        if (!ApiIdentity.TryGetUserId(principal, out var userId) ||
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

    private static DeveloperBookSummary ToResponse(BookListItem value) =>
        new(value.Id, value.Title, value.Author, value.ChapterCount);

    private static DeveloperBookDetail ToResponse(BookDetail value) =>
        new(value.Id, value.Title, value.Author, value.Chapters.Count);

    internal readonly record struct DeveloperIdentity(
        Guid UserId,
        Guid ApplicationId,
        Guid ApiKeyId);
}
