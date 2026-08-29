using InkFlow.BuildingBlocks.Security;

namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>
/// 将安全审计事实追加到 PostgreSQL。普通应用层不提供更新/删除路径；过期清理只能走受控 retention seam。
/// </summary>
public sealed class PersistentAuditEventSink(AuditDbContext db) : IAuditEventSink
{
    public async ValueTask AppendAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.Events.Add(AuditEventMapper.ToEntity(auditEvent));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
