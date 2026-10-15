using System.Security.Claims;
using InkFlow.Modules.Reading.Application;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Api;

public sealed record ShelfStatusRequest(string? Status);

public sealed record ReadingProgressRequest(
    Guid ChapterId,
    int ParagraphIndex,
    int ProgressPercent);

public sealed record ReaderPreferenceRequest(
    int? FontSizePercent,
    int? LineHeightPercent,
    string? Theme);

public static class ReadingEndpointResults
{
    public static void Map(RouteGroupBuilder api)
    {
        // ---- 用户阅读状态(用户数据严格按认证主体隔离)----

        var reading = api.MapGroup("/me/reading")
            .RequireAuthorization();

        reading.MapGet("/shelf", async (
            int? limit,
            ClaimsPrincipal principal,
            IReadingStateService state,
            CancellationToken ct) =>
        {
            if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await state
                .ListShelfAsync(userId, limit ?? ReadingStateService.DefaultPageSize, ct)
                .ConfigureAwait(false));
        });

        reading.MapPut("/shelf/{bookId:guid}", async (
            Guid bookId,
            ShelfStatusRequest? request,
            ClaimsPrincipal principal,
            IReadingStateService state,
            CancellationToken ct) =>
        {
            if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (!ReadingEndpointResults.TryParseShelfStatus(request?.Status, out var status))
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }

            var result = await state.PutShelfAsync(userId, bookId, status, ct).ConfigureAwait(false);
            return ReadingEndpointResults.FromResult(result);
        });

        reading.MapDelete("/shelf/{bookId:guid}", async (
            Guid bookId,
            ClaimsPrincipal principal,
            IReadingStateService state,
            CancellationToken ct) =>
        {
            if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var status = await state.RemoveShelfAsync(userId, bookId, ct).ConfigureAwait(false);
            return ReadingEndpointResults.FromStatus(status);
        });

        reading.MapGet("/history", async (
            int? limit,
            ClaimsPrincipal principal,
            IReadingStateService state,
            CancellationToken ct) =>
        {
            if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await state
                .ListHistoryAsync(userId, limit ?? ReadingStateService.DefaultPageSize, ct)
                .ConfigureAwait(false));
        });

        reading.MapGet("/progress/{bookId:guid}", async (
            Guid bookId,
            ClaimsPrincipal principal,
            IReadingStateService state,
            CancellationToken ct) =>
        {
            if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            var progress = await state.GetProgressAsync(userId, bookId, ct).ConfigureAwait(false);
            return progress is null ? Results.NotFound() : Results.Ok(progress);
        });

        reading.MapPut("/progress/{bookId:guid}", async (
            Guid bookId,
            ReadingProgressRequest? request,
            ClaimsPrincipal principal,
            IReadingStateService state,
            CancellationToken ct) =>
        {
            if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (request is null)
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }

            var result = await state.SaveProgressAsync(
                userId,
                bookId,
                request.ChapterId,
                request.ParagraphIndex,
                request.ProgressPercent,
                ct).ConfigureAwait(false);
            return ReadingEndpointResults.FromResult(result);
        });

        reading.MapGet("/preferences", async (
            ClaimsPrincipal principal,
            IReadingStateService state,
            CancellationToken ct) =>
        {
            if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await state.GetPreferencesAsync(userId, ct).ConfigureAwait(false));
        });

        reading.MapPut("/preferences", async (
            ReaderPreferenceRequest? request,
            ClaimsPrincipal principal,
            IReadingStateService state,
            CancellationToken ct) =>
        {
            if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
            {
                return Results.Unauthorized();
            }

            if (!ReadingEndpointResults.TryParseTheme(request?.Theme, out var theme))
            {
                return Results.BadRequest(new { error = "invalid_request" });
            }

            var result = await state.UpdatePreferencesAsync(
                userId,
                request?.FontSizePercent,
                request?.LineHeightPercent,
                theme,
                ct).ConfigureAwait(false);
            return ReadingEndpointResults.FromResult(result);
        });
    }

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        return Guid.TryParse(principal.FindFirstValue("sub"), out userId) &&
               userId != Guid.Empty;
    }

    public static IResult FromResult<T>(ReadingOperationResult<T> result) =>
        result.Status switch
        {
            ReadingResultStatus.Success => Results.Ok(result.Value),
            ReadingResultStatus.NotFound => Results.NotFound(),
            _ => Results.BadRequest(new { error = "invalid_request" }),
        };

    public static IResult FromStatus(ReadingResultStatus status) =>
        status switch
        {
            ReadingResultStatus.Success => Results.NoContent(),
            ReadingResultStatus.NotFound => Results.NotFound(),
            _ => Results.BadRequest(new { error = "invalid_request" }),
        };

    public static bool TryParseShelfStatus(
        string? raw,
        out ShelfStatus status)
    {
        status = ShelfStatus.Reading;
        return raw is null ||
               (Enum.TryParse(raw, ignoreCase: true, out status) && Enum.IsDefined(status));
    }

    public static bool TryParseTheme(
        string? raw,
        out ReaderTheme? theme)
    {
        theme = null;
        if (raw is null)
        {
            return true;
        }

        if (!Enum.TryParse<ReaderTheme>(raw, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            return false;
        }

        theme = parsed;
        return true;
    }
}
