using System.Security.Claims;
using InkFlow.Modules.Library.Application;
using Microsoft.AspNetCore.Http.Features;

namespace InkFlow.Api;

public sealed record PrivateBookRequest(string? Title, string? Author);

public static class PrivateLibraryEndpointResults
{
    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        return Guid.TryParse(principal.FindFirstValue("sub"), out userId) &&
               userId != Guid.Empty;
    }

    public static IResult FromOperation<T>(
        PrivateLibraryOperationResult<T> result,
        Func<T, IResult>? onSuccess = null) =>
        result.Status switch
        {
            PrivateLibraryResultStatus.Success when result.Value is not null =>
                onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value),
            PrivateLibraryResultStatus.NotFound => Results.NotFound(),
            _ => Results.BadRequest(new { error = "invalid_request" }),
        };

    public static IResult FromStatus(PrivateLibraryResultStatus status) =>
        status switch
        {
            PrivateLibraryResultStatus.Success => Results.NoContent(),
            PrivateLibraryResultStatus.NotFound => Results.NotFound(),
            _ => Results.BadRequest(new { error = "invalid_request" }),
        };

    public static IResult FromContentOperation<T>(
        PrivateLibraryOperationResult<T> result,
        Func<T, IResult>? onSuccess = null) =>
        result.Status switch
        {
            PrivateLibraryResultStatus.Success when result.Value is not null =>
                onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value),
            PrivateLibraryResultStatus.NotFound => Results.NotFound(),
            PrivateLibraryResultStatus.UnsupportedFormat =>
                Results.BadRequest(new { error = "unsupported_format" }),
            PrivateLibraryResultStatus.FileTooLarge =>
                Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
            PrivateLibraryResultStatus.InvalidFile =>
                Results.BadRequest(new { error = "invalid_file" }),
            _ => Results.BadRequest(new { error = "invalid_request" }),
        };
}

public static class PrivateLibraryEndpointMapping
{
    public static void MapPrivateLibraryEndpoints(this RouteGroupBuilder api)
    {
        var privateLibrary = api.MapGroup("/me/private-library")
            .RequireAuthorization();

        privateLibrary.MapPost("/import", async (
            HttpRequest request,
            ClaimsPrincipal principal,
            IPrivateLibraryContentService content,
            CancellationToken ct) =>
        {
            if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (request.ContentLength is > PrivateBookImportLimits.MaxMultipartRequestBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var bodySize = request.HttpContext.Features
                .Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySize is { IsReadOnly: false })
            {
                bodySize.MaxRequestBodySize = PrivateBookImportLimits.MaxMultipartRequestBytes;
            }

            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "multipart_file_required" });
            }

            IFormCollection form;
            try
            {
                form = await request.ReadFormAsync(ct).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                return Results.BadRequest(new { error = "invalid_file" });
            }

            if (form.Files.Count != 1)
            {
                return Results.BadRequest(new { error = "one_file_required" });
            }

            var file = form.Files[0];
            if (file.Length > PrivateBookImportLimits.MaxUploadBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            await using var stream = file.OpenReadStream();
            var result = await content.ImportAsync(
                    userId,
                    file.FileName,
                    file.ContentType,
                    stream,
                    ct)
                .ConfigureAwait(false);
            return PrivateLibraryEndpointResults.FromContentOperation(
                result,
                value => Results.Created(
                    $"/api/v1/me/private-library/books/{value.Book.PrivateBookId:D}",
                    value));
        });

        privateLibrary.MapGet("/books", async (
            int? limit,
            ClaimsPrincipal principal,
            IPrivateLibraryService library,
            CancellationToken ct) =>
        {
            if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await library
                .ListAsync(userId, limit ?? PrivateLibraryService.DefaultPageSize, ct)
                .ConfigureAwait(false));
        });

        privateLibrary.MapPost("/books", async (
            PrivateBookRequest? request,
            ClaimsPrincipal principal,
            IPrivateLibraryService library,
            CancellationToken ct) =>
        {
            if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }

            var result = await library.CreateAsync(
                    userId,
                    request.Title,
                    request.Author,
                    ct)
                .ConfigureAwait(false);
            return PrivateLibraryEndpointResults.FromOperation(
                result,
                value => Results.Created(
                    $"/api/v1/me/private-library/books/{value.PrivateBookId:D}",
                    value));
        });

        privateLibrary.MapGet("/books/{privateBookId:guid}", async (
            Guid privateBookId,
            ClaimsPrincipal principal,
            IPrivateLibraryService library,
            CancellationToken ct) =>
        {
            if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var book = await library.GetAsync(userId, privateBookId, ct).ConfigureAwait(false);
            return book is null ? Results.NotFound() : Results.Ok(book);
        });

        privateLibrary.MapGet("/books/{privateBookId:guid}/chapters", async (
            Guid privateBookId,
            ClaimsPrincipal principal,
            IPrivateLibraryContentService content,
            HttpResponse response,
            CancellationToken ct) =>
        {
            if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            response.Headers.CacheControl = "private, no-store";
            return Results.Ok(await content
                .ListChaptersAsync(userId, privateBookId, ct)
                .ConfigureAwait(false));
        });

        privateLibrary.MapGet(
            "/books/{privateBookId:guid}/chapters/{privateChapterId:guid}",
            async (
                Guid privateBookId,
                Guid privateChapterId,
                ClaimsPrincipal principal,
                IPrivateLibraryContentService content,
                HttpResponse response,
                CancellationToken ct) =>
            {
                if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
                {
                    return Results.Unauthorized();
                }

                var chapter = await content
                    .GetChapterAsync(userId, privateBookId, privateChapterId, ct)
                    .ConfigureAwait(false);
                if (chapter is null)
                {
                    return Results.NotFound();
                }

                response.Headers.CacheControl = "private, no-store";
                return Results.Ok(chapter);
            });

        privateLibrary.MapGet("/books/{privateBookId:guid}/export", async (
            Guid privateBookId,
            string? format,
            ClaimsPrincipal principal,
            IPrivateLibraryContentService content,
            HttpResponse response,
            CancellationToken ct) =>
        {
            if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await content
                .ExportAsync(userId, privateBookId, format, ct)
                .ConfigureAwait(false);
            if (result.Status == PrivateLibraryResultStatus.Success && result.Value is not null)
            {
                response.Headers.CacheControl = "private, no-store";
                return Results.File(
                    result.Value.Content,
                    result.Value.ContentType,
                    result.Value.FileName,
                    enableRangeProcessing: false);
            }

            return PrivateLibraryEndpointResults.FromContentOperation(result);
        });

        privateLibrary.MapPut("/books/{privateBookId:guid}", async (
            Guid privateBookId,
            PrivateBookRequest? request,
            ClaimsPrincipal principal,
            IPrivateLibraryService library,
            CancellationToken ct) =>
        {
            if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }

            var result = await library.UpdateAsync(
                    userId,
                    privateBookId,
                    request.Title,
                    request.Author,
                    ct)
                .ConfigureAwait(false);
            return PrivateLibraryEndpointResults.FromOperation(result);
        });

        privateLibrary.MapDelete("/books/{privateBookId:guid}", async (
            Guid privateBookId,
            ClaimsPrincipal principal,
            IPrivateLibraryService library,
            CancellationToken ct) =>
        {
            if (!PrivateLibraryEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var status = await library.DeleteAsync(userId, privateBookId, ct).ConfigureAwait(false);
            return PrivateLibraryEndpointResults.FromStatus(status);
        });
    }
}
