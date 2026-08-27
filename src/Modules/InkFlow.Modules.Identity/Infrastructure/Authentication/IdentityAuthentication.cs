using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InkFlow.Modules.Identity.Infrastructure.Authentication;

public static class IdentityAuthenticationDefaults
{
    public const string Scheme = "InkFlowBearer";
    public const string LegadoScheme = "InkFlowLegadoToken";
    public const string LegadoTokenHeader = "X-InkFlow-Legado-Token";
    public const string LegadoTokenIdClaim = "legado_token_id";
    public const string LegadoScopeClaim = "legado_scope";
    public const string SessionIdClaim = "sid";
}

/// <summary>
/// 数据库支持的 opaque Bearer 认证。短期访问令牌的摘要落库，因此登出/停用用户可立即失效，
/// 不把可验证的完整长寿命秘密放进 JWT 或日志。
/// </summary>
public sealed class OpaqueBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IIdentityService _identity;

    public OpaqueBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IIdentityService identity)
        : base(options, logger, encoder)
    {
        _identity = identity;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return AuthenticateResult.NoResult();
        }

        if (!AuthenticationHeaderValue.TryParse(header, out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter) ||
            authorization.Parameter.Length > 512)
        {
            return AuthenticateResult.Fail("invalid bearer token.");
        }

        try
        {
            var identity = await _identity
                .ValidateAccessTokenAsync(authorization.Parameter, Context.RequestAborted)
                .ConfigureAwait(false);
            if (identity is null)
            {
                return AuthenticateResult.Fail("invalid bearer token.");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, identity.UserId.ToString()),
                new Claim("sub", identity.UserId.ToString()),
                new Claim(ClaimTypes.Email, identity.Email),
                new Claim("email", identity.Email),
                new Claim(ClaimTypes.Role, identity.Role.ToString()),
                new Claim("role", identity.Role.ToString()),
                new Claim(IdentityAuthenticationDefaults.SessionIdClaim, identity.SessionId.ToString()),
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "identity access-token validation failed");
            return AuthenticateResult.Fail("authentication unavailable.");
        }
    }
}

/// <summary>
/// Personal Legado Token 认证。令牌只从专用 header 读取，不接受 query string 或 URL 参数。
/// </summary>
public sealed class LegadoTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ILegadoAccessTokenService _tokens;

    public LegadoTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ILegadoAccessTokenService tokens)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var values = Request.Headers[IdentityAuthenticationDefaults.LegadoTokenHeader];
        if (values.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        if (values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]) ||
            values[0]!.Length > 512)
        {
            return AuthenticateResult.Fail("invalid Legado token.");
        }

        try
        {
            var authenticated = await _tokens
                .ValidateAsync(
                    values[0]!,
                    LegadoTokenScope.Read,
                    Context.RequestAborted)
                .ConfigureAwait(false);
            if (authenticated is null)
            {
                return AuthenticateResult.Fail("invalid Legado token.");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, authenticated.UserId.ToString()),
                new Claim("sub", authenticated.UserId.ToString()),
                new Claim(
                    IdentityAuthenticationDefaults.LegadoTokenIdClaim,
                    authenticated.TokenId.ToString()),
                new Claim(
                    IdentityAuthenticationDefaults.LegadoScopeClaim,
                    authenticated.Scope.ToString().ToLowerInvariant()),
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, IdentityAuthenticationDefaults.LegadoScheme));
            return AuthenticateResult.Success(
                new AuthenticationTicket(principal, IdentityAuthenticationDefaults.LegadoScheme));
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Legado token validation failed");
            return AuthenticateResult.Fail("authentication unavailable.");
        }
    }
}
