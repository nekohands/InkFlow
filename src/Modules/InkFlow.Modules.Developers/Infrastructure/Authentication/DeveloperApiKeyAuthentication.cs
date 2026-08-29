using System.Security.Claims;
using System.Text.Encodings.Web;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InkFlow.Modules.Developers.Infrastructure.Authentication;

public static class DeveloperAuthenticationDefaults
{
    public const string Scheme = "InkFlowDeveloperApiKey";
    public const string ApiKeyHeader = "X-InkFlow-Api-Key";
    public const string ApplicationIdClaim = "client_id";
    public const string ApiKeyIdClaim = "developer_api_key_id";
    public const string ScopeClaim = "scope";
    public const string EnvironmentClaim = "environment";
}

/// <summary>
/// Developer API 的专用 API Key 认证。密钥只从 Header 读取，不接受 URL、Query 或 Bearer 位置。
/// </summary>
public sealed class DeveloperApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDeveloperApiKeyValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var values = Request.Headers[DeveloperAuthenticationDefaults.ApiKeyHeader];
        if (values.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]) || values[0]!.Length > 512)
        {
            return AuthenticateResult.Fail("invalid developer API key.");
        }

        try
        {
            var authenticated = await validator
                .ValidateAsync(values[0]!, Context.RequestAborted)
                .ConfigureAwait(false);
            if (authenticated is null)
            {
                return AuthenticateResult.Fail("invalid developer API key.");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, authenticated.UserId.ToString()),
                new Claim("sub", authenticated.UserId.ToString()),
                new Claim(
                    DeveloperAuthenticationDefaults.ApplicationIdClaim,
                    authenticated.ApplicationId.ToString()),
                new Claim(
                    DeveloperAuthenticationDefaults.ApiKeyIdClaim,
                    authenticated.KeyId.ToString()),
                new Claim(DeveloperAuthenticationDefaults.ScopeClaim, authenticated.Scope),
                new Claim(
                    DeveloperAuthenticationDefaults.EnvironmentClaim,
                    authenticated.Environment.ToString().ToLowerInvariant()),
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                DeveloperAuthenticationDefaults.Scheme));
            return AuthenticateResult.Success(new AuthenticationTicket(
                principal,
                DeveloperAuthenticationDefaults.Scheme));
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "developer API key validation failed");
            return AuthenticateResult.Fail("developer API key authentication unavailable.");
        }
    }
}
