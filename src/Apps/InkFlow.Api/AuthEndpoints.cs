using Microsoft.AspNetCore.Http.Features;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Identity.Infrastructure.Authentication;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json.Serialization;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Api;

public sealed record RegisterRequest(string? Email, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UpdateProfileRequest(string? DisplayName);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record RefreshRequest(
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string Role);

public sealed record AccountProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

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
    public static void Map(RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/register", async (
            RegisterRequest request,
            IIdentityService identity,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            var result = await identity.RegisterAsync(
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                ct);
            return AuthEndpointResults.FromIdentityResult(result, clock);
        });

        auth.MapPost("/login", async (
            LoginRequest request,
            IIdentityService identity,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            var result = await identity.LoginAsync(
                request.Email ?? string.Empty,
                request.Password ?? string.Empty,
                ct);
            return AuthEndpointResults.FromIdentityResult(result, clock);
        });

        auth.MapPost("/refresh", async (
            RefreshRequest request,
            IIdentityService identity,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            var result = await identity.RefreshAsync(request.RefreshToken ?? string.Empty, ct);
            return AuthEndpointResults.FromIdentityResult(result, clock);
        });

        auth.MapPost("/logout", async (
            ClaimsPrincipal principal,
            IIdentityService identity,
            CancellationToken ct) =>
        {
            var rawSessionId = principal.FindFirstValue(IdentityAuthenticationDefaults.SessionIdClaim);
            if (!Guid.TryParse(rawSessionId, out var sessionId))
            {
                return Results.Unauthorized();
            }

            await identity.LogoutAsync(sessionId, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        auth.MapGet("/me", (ClaimsPrincipal principal) =>
            AuthEndpointResults.Current(principal)).RequireAuthorization();
    }

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
            // 重放响应与普通失效同样返回 401，但使用独立错误码：客户端必须丢弃本地令牌并重新登录，
            // 运维/告警侧也可据此识别凭证泄露信号。
            IdentityResultStatus.RefreshTokenReplayDetected =>
                Error("refresh_token_replay_detected", StatusCodes.Status401Unauthorized),
            _ => Error("authentication_failed", StatusCodes.Status401Unauthorized),
        };
    }

    public static IResult Current(ClaimsPrincipal principal)
    {
        var email = principal.FindFirstValue("email");
        var role = principal.FindFirstValue("role");
        return !ApiIdentity.TryGetUserId(principal, out var parsedUserId) ||
               string.IsNullOrWhiteSpace(email) ||
               string.IsNullOrWhiteSpace(role)
            ? Results.Unauthorized()
            : Results.Ok(new AuthUserResponse(parsedUserId, email, role));
    }

    private static IResult Error(string code, int statusCode) =>
        Results.Json(new { error = code }, statusCode: statusCode);
}

public static class AccountEndpointResults
{
    public static void Map(RouteGroupBuilder api)
    {
        var account = api.MapGroup("/me")
            .RequireAuthorization();

        account.MapGet("/profile", async (
            ClaimsPrincipal principal,
            IIdentityService identity,
            CancellationToken ct) =>
        {
            if (!AccountEndpointResults.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var profile = await identity.GetProfileAsync(userId, ct).ConfigureAwait(false);
            return profile is null
                ? Results.Unauthorized()
                : Results.Ok(AccountEndpointResults.ToResponse(profile));
        });

        account.MapPut("/profile", async (
            UpdateProfileRequest? request,
            ClaimsPrincipal principal,
            IIdentityService identity,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!AccountEndpointResults.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await identity
                .UpdateProfileAsync(userId, request?.DisplayName, ct)
                .ConfigureAwait(false);
            var statusCode = result.Status switch
            {
                ProfileResultStatus.Success => StatusCodes.Status200OK,
                ProfileResultStatus.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            await AccountEndpointResults.AppendAccountAuditAsync(
                "identity.profile.update",
                principal,
                httpContext,
                auditSink,
                clock,
                statusCode,
                result.IsSuccess,
                ct).ConfigureAwait(false);
            return AccountEndpointResults.FromProfile(result);
        });

        account.MapGet("/profile/avatar", async (
            ClaimsPrincipal principal,
            IIdentityService identity,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (!AccountEndpointResults.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var avatar = await identity.GetAvatarAsync(userId, ct).ConfigureAwait(false);
            if (avatar is null)
            {
                return Results.NotFound(new { error = "avatar_not_found" });
            }

            httpContext.Response.Headers.CacheControl = "private, no-store";
            httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(
                avatar.Content,
                avatar.ContentType,
                enableRangeProcessing: false);
        });

        account.MapPut("/profile/avatar", async (
            HttpRequest request,
            ClaimsPrincipal principal,
            IIdentityService identity,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!AccountEndpointResults.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            if (request.ContentLength is > AvatarImage.MaxMultipartRequestBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var bodySize = request.HttpContext.Features
                .Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySize is { IsReadOnly: false })
            {
                bodySize.MaxRequestBodySize = AvatarImage.MaxMultipartRequestBytes;
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
                return Results.BadRequest(new { error = "invalid_image" });
            }

            if (form.Files.Count != 1)
            {
                return Results.BadRequest(new { error = "one_file_required" });
            }

            var file = form.Files[0];
            if (file.Length > AvatarImage.MaxUploadBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            await using var stream = file.OpenReadStream();
            var result = await identity.UploadAvatarAsync(userId, stream, ct).ConfigureAwait(false);
            var statusCode = result.Status switch
            {
                AvatarResultStatus.Success => StatusCodes.Status204NoContent,
                AvatarResultStatus.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            await AccountEndpointResults.AppendAccountAuditAsync(
                "identity.avatar.update",
                principal,
                httpContext,
                auditSink,
                clock,
                statusCode,
                result.IsSuccess,
                ct).ConfigureAwait(false);
            return AccountEndpointResults.FromAvatarUpload(result);
        });

        account.MapPost("/password", async (
            ChangePasswordRequest? request,
            ClaimsPrincipal principal,
            IIdentityService identity,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!AccountEndpointResults.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await identity
                .ChangePasswordAsync(
                    userId,
                    request?.CurrentPassword ?? string.Empty,
                    request?.NewPassword ?? string.Empty,
                    ct)
                .ConfigureAwait(false);
            var statusCode = result.Status switch
            {
                PasswordChangeResultStatus.Success => StatusCodes.Status204NoContent,
                PasswordChangeResultStatus.InvalidCredentials or PasswordChangeResultStatus.NotFound =>
                    StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status400BadRequest,
            };
            await AccountEndpointResults.AppendAccountAuditAsync(
                "identity.password.change",
                principal,
                httpContext,
                auditSink,
                clock,
                statusCode,
                result.IsSuccess,
                ct).ConfigureAwait(false);
            return AccountEndpointResults.FromPasswordChange(result);
        });
    }

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
        => ApiIdentity.TryGetUserId(principal, out userId);

    public static AccountProfileResponse ToResponse(IdentityProfile profile) =>
        new(
            profile.Id,
            profile.Email,
            profile.DisplayName,
            profile.Role.ToString(),
            profile.Status.ToString(),
            profile.CreatedAt,
            profile.UpdatedAt);

    public static IResult FromProfile(ProfileOperationResult result) => result.Status switch
    {
        ProfileResultStatus.Success when result.Profile is not null =>
            Results.Ok(ToResponse(result.Profile)),
        ProfileResultStatus.NotFound => Results.NotFound(new { error = "not_found" }),
        _ => Results.BadRequest(new { error = "invalid_request" }),
    };

    public static IResult FromPasswordChange(PasswordChangeOperationResult result) => result.Status switch
    {
        PasswordChangeResultStatus.Success => Results.NoContent(),
        PasswordChangeResultStatus.InvalidCredentials =>
            Results.Json(new { error = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized),
        PasswordChangeResultStatus.NotFound =>
            Results.Json(new { error = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized),
        _ => Results.BadRequest(new { error = "invalid_request" }),
    };

    public static IResult FromAvatarUpload(AvatarOperationResult result) => result.Status switch
    {
        AvatarResultStatus.Success => Results.NoContent(),
        AvatarResultStatus.NotFound => Results.NotFound(new { error = "not_found" }),
        _ => Results.BadRequest(new { error = "invalid_image" }),
    };

    private static ValueTask AppendAccountAuditAsync(
        string action,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        int statusCode,
        bool isSuccess,
        CancellationToken cancellationToken) =>
        auditSink.AppendAsync(
            CreateAudit(
                action,
                principal,
                httpContext,
                clock,
                statusCode,
                isSuccess ? "success" : "client_error"),
            cancellationToken);

    public static AuditEvent CreateAudit(
        string action,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        TimeProvider clock,
        int statusCode,
        string outcome) =>
        AuditEvent.Create(
            action,
            httpContext.Request.Path,
            outcome,
            statusCode,
            clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: principal.FindFirstValue("sub"),
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: "account-settings");
}
