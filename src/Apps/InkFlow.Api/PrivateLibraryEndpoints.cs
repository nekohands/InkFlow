using System.Security.Claims;
using InkFlow.Modules.Library.Application;

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
}

public static class PrivateLibraryEndpointMapping
{
    public static void MapPrivateLibraryEndpoints(this RouteGroupBuilder api)
    {
        var privateLibrary = api.MapGroup("/me/private-library")
            .RequireAuthorization();

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
