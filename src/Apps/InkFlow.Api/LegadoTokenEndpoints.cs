using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Legado.Application;

namespace InkFlow.Api;

public sealed record CreateLegadoTokenRequest(string? Name);

public sealed record LegadoTokenResponse(
    Guid Id,
    string Name,
    string Prefix,
    string Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt);

public sealed record LegadoTokenIssueResponse(
    Guid Id,
    string Name,
    string Prefix,
    string Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string Token,
    JsonElement BookSource);

/// <summary>Personal Legado Token 管理结果与命令审计。</summary>
public static class LegadoTokenEndpointResults
{
    public static void Map(RouteGroupBuilder api)
    {
        // ---- Personal Legado Token 管理(使用 Web Access Token,令牌只签发一次)----

        var personalLegadoTokens = api.MapGroup("/me/legado")
            .RequireAuthorization();

        personalLegadoTokens.MapPost("/tokens", async (
            CreateLegadoTokenRequest? request,
            ClaimsPrincipal principal,
            ILegadoAccessTokenService tokens,
            ILegadoRuleGenerator legadoRuleGenerator,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!LegadoTokenEndpointResults.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var result = await tokens.IssueAsync(userId, request?.Name, ct).ConfigureAwait(false);
            if (!result.IsSuccess || result.Issue is null)
            {
                return LegadoTokenEndpointResults.FromIssueFailure(result.Status);
            }

            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            using var document = JsonDocument.Parse(
                legadoRuleGenerator.Generate(baseUrl, result.Issue.RawToken));
            var response = LegadoTokenEndpointResults.ToIssueResponse(
                result.Issue,
                document.RootElement.Clone());
            return LegadoTokenEndpointResults.Issue(
                response,
                httpContext,
                auditSink,
                clock,
                ct);
        });

        personalLegadoTokens.MapGet("/tokens", async (
            ClaimsPrincipal principal,
            ILegadoAccessTokenService tokens,
            CancellationToken ct) =>
        {
            if (!LegadoTokenEndpointResults.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var values = await tokens.ListAsync(userId, ct).ConfigureAwait(false);
            return Results.Ok(values.Select(LegadoTokenEndpointResults.ToResponse));
        });

        personalLegadoTokens.MapDelete("/tokens/{tokenId:guid}", async (
            Guid tokenId,
            ClaimsPrincipal principal,
            ILegadoAccessTokenService tokens,
            HttpContext httpContext,
            IAuditEventSink auditSink,
            TimeProvider clock,
            CancellationToken ct) =>
        {
            if (!LegadoTokenEndpointResults.TryGetUserId(principal, out var userId))
            {
                return (IResult)Results.Unauthorized();
            }

            var status = await tokens.RevokeAsync(userId, tokenId, ct).ConfigureAwait(false);
            if (status != LegadoTokenResultStatus.Success)
            {
                return LegadoTokenEndpointResults.FromRevokeStatus(status);
            }

            return LegadoTokenEndpointResults.Revoke(
                tokenId,
                principal,
                httpContext,
                auditSink,
                clock,
                ct);
        });
    }

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        var raw = principal.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId) && userId != Guid.Empty;
    }

    public static LegadoTokenResponse ToResponse(LegadoTokenInfo info) => new(
        info.Id,
        info.Name,
        info.Prefix,
        info.Scope.ToString().ToLowerInvariant(),
        info.CreatedAt,
        info.ExpiresAt,
        info.RevokedAt);

    public static LegadoTokenIssueResponse ToIssueResponse(
        LegadoTokenIssue issue,
        JsonElement bookSource) => new(
            issue.Info.Id,
            issue.Info.Name,
            issue.Info.Prefix,
            issue.Info.Scope.ToString().ToLowerInvariant(),
            issue.Info.CreatedAt,
            issue.Info.ExpiresAt,
            issue.RawToken,
            bookSource);

    public static IResult Issue(
        LegadoTokenIssueResponse response,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var auditEvent = AuditEvent.Create(
            action: "identity.legado_token.issue",
            resource: "/api/v1/me/legado/tokens",
            outcome: "success",
            statusCode: StatusCodes.Status201Created,
            occurredAt: clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: httpContext.User.FindFirstValue("sub"),
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: $"legado-token:{response.Id};prefix:{response.Prefix};scope:{response.Scope}");

        return new AuditedResult(
            auditEvent,
            auditSink,
            cancellationToken,
            () => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    public static IResult Revoke(
        Guid tokenId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IAuditEventSink auditSink,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var actorId = principal.FindFirstValue("sub");
        var auditEvent = AuditEvent.Create(
            action: "identity.legado_token.revoke",
            resource: $"/api/v1/me/legado/tokens/{tokenId}",
            outcome: "success",
            statusCode: StatusCodes.Status204NoContent,
            occurredAt: clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: $"legado-token:{tokenId}");

        return new AuditedResult(
            auditEvent,
            auditSink,
            cancellationToken,
            () => Results.NoContent());
    }

    public static IResult FromIssueFailure(LegadoTokenResultStatus status) => status switch
    {
        LegadoTokenResultStatus.NotFound => Results.NotFound(new { error = "not_found" }),
        _ => Results.BadRequest(new { error = "invalid_request" }),
    };

    public static IResult FromRevokeStatus(LegadoTokenResultStatus status) => status switch
    {
        LegadoTokenResultStatus.Success => throw new InvalidOperationException(
            "successful revocation must be wrapped with an audit result."),
        LegadoTokenResultStatus.NotFound => Results.NotFound(new { error = "not_found" }),
        _ => Results.BadRequest(new { error = "invalid_request" }),
    };
}
