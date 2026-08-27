using System.Security.Claims;
using System.Text.Json.Serialization;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Api;

public sealed record RegisterRequest(string? Email, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshRequest(
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string Role);

public sealed record AuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] long ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("refresh_token_expires_at")] DateTimeOffset RefreshTokenExpiresAt,
    AuthUserResponse User)
{
    public static AuthTokenResponse From(AuthSession session, DateTimeOffset now) =>
        new(
            session.AccessToken,
            "Bearer",
            Math.Max(0, (long)(session.AccessTokenExpiresAt - now).TotalSeconds),
            session.RefreshToken,
            session.RefreshTokenExpiresAt,
            new AuthUserResponse(session.UserId, session.Email, session.Role.ToString()));
}

public static class AuthEndpointResults
{
    public static IResult FromIdentityResult(
        IdentityOperationResult result,
        TimeProvider clock)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(AuthTokenResponse.From(result.Session!, clock.GetUtcNow()));
        }

        return result.Status switch
        {
            IdentityResultStatus.InvalidRequest => Error("invalid_request", StatusCodes.Status400BadRequest),
            IdentityResultStatus.EmailAlreadyRegistered =>
                Error("email_already_registered", StatusCodes.Status409Conflict),
            IdentityResultStatus.InvalidCredentials or IdentityResultStatus.InvalidRefreshToken =>
                Error("invalid_credentials", StatusCodes.Status401Unauthorized),
            _ => Error("authentication_failed", StatusCodes.Status401Unauthorized),
        };
    }

    public static IResult Current(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue("sub");
        var email = principal.FindFirstValue("email");
        var role = principal.FindFirstValue("role");
        return !Guid.TryParse(userId, out var parsedUserId) ||
               string.IsNullOrWhiteSpace(email) ||
               string.IsNullOrWhiteSpace(role)
            ? Results.Unauthorized()
            : Results.Ok(new AuthUserResponse(parsedUserId, email, role));
    }

    private static IResult Error(string code, int statusCode) =>
        Results.Json(new { error = code }, statusCode: statusCode);
}
