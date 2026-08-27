using InkFlow.BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InkFlow.BuildingBlocks.Persistence;

public sealed class AuditEventEntityConfiguration : IEntityTypeConfiguration<AuditEventEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventEntity> builder)
    {
        builder.ToTable("events");
        builder.HasKey(eventEntity => eventEntity.Id);

        builder.Property(eventEntity => eventEntity.ActorType).HasMaxLength(64).IsRequired();
        builder.Property(eventEntity => eventEntity.ActorId).HasMaxLength(256);
        builder.Property(eventEntity => eventEntity.Action).HasMaxLength(128).IsRequired();
        builder.Property(eventEntity => eventEntity.Resource).HasMaxLength(512).IsRequired();
        builder.Property(eventEntity => eventEntity.Outcome).HasMaxLength(64).IsRequired();
        builder.Property(eventEntity => eventEntity.StatusCode).IsRequired();
        builder.Property(eventEntity => eventEntity.Reason).HasMaxLength(512);
        builder.Property(eventEntity => eventEntity.TraceId).HasMaxLength(128);
        builder.Property(eventEntity => eventEntity.Reference).HasMaxLength(512);

        builder.HasIndex(eventEntity => new { eventEntity.OccurredAt, eventEntity.Id });
    }
}

/// <summary>Security BuildingBlock 模型与数据库行之间的显式映射。</summary>
public static class AuditEventMapper
{
    public static AuditEventEntity ToEntity(AuditEvent auditEvent) =>
        new()
        {
            Id = auditEvent.Id,
            OccurredAt = auditEvent.OccurredAt,
            ActorType = auditEvent.ActorType,
            ActorId = auditEvent.ActorId,
            Action = auditEvent.Action,
            Resource = auditEvent.Resource,
            Outcome = auditEvent.Outcome,
            StatusCode = auditEvent.StatusCode,
            Reason = auditEvent.Reason,
            TraceId = auditEvent.TraceId,
            Reference = auditEvent.Reference,
        };
}
