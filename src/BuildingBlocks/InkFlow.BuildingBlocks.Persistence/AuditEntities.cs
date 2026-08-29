namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>审计事实的数据库行。普通写入只通过追加式 sink 创建，过期删除由受控 retention seam 执行。</summary>
public sealed class AuditEventEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string ActorType { get; set; } = null!;
    public string? ActorId { get; set; }
    public string Action { get; set; } = null!;
    public string Resource { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public int StatusCode { get; set; }
    public string? Reason { get; set; }
    public string? TraceId { get; set; }
    public string? Reference { get; set; }
}
