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

    private sealed class AuditedResult(
        AuditEvent auditEvent,
        IAuditEventSink auditSink,
        CancellationToken cancellationToken,
        Func<IResult> resultFactory) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            await auditSink.AppendAsync(auditEvent, cancellationToken).ConfigureAwait(false);
            await resultFactory().ExecuteAsync(httpContext).ConfigureAwait(false);
        }
    }
}
