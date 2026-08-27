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
