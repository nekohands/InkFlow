using InkFlow.BuildingBlocks.Security;

namespace InkFlow.Api;

/// <summary>审计成功落库后才创建并执行响应；失败和取消由调用方处理。</summary>
internal sealed class AuditedResult(
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
