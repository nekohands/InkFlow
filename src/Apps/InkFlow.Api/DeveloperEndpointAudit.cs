using System.Diagnostics;
using InkFlow.BuildingBlocks.Security;

namespace InkFlow.Api;

internal static class DeveloperEndpointAudit
{
    public static async Task RecordAsync(
        IAuditEventSink auditSink,
        TimeProvider clock,
        HttpContext httpContext,
        string action,
        string resource,
        Guid actorId,
        int statusCode,
        string outcome,
        string reference,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        var auditEvent = AuditEvent.Create(
            action,
            resource,
            outcome,
            statusCode,
            clock.GetUtcNow(),
            actorType: "authenticated",
            actorId: actorId.ToString("D"),
            reason: reason,
            traceId: Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier,
            reference: reference);
        try
        {
            await auditSink.AppendAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 审计故障不回滚已经完成的密钥/套餐命令；主请求审计中间件仍会保留结果。
            var logger = httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("InkFlow.Api.DeveloperEndpointAudit");
            logger.LogError(exception, "developer command audit failed for {AuditEventId}", auditEvent.Id);
        }
    }
}
