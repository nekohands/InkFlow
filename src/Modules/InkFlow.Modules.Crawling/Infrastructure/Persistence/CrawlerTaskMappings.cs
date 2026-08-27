using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

public sealed class CrawlerTaskEntityConfiguration : IEntityTypeConfiguration<CrawlerTaskEntity>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<CrawlerTaskEntity> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.SourceId).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Capability).IsRequired();
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.AttemptCount).IsRequired();
        builder.Property(t => t.MaxAttempts).IsRequired();
        builder.Property(t => t.LeaseOwner).HasMaxLength(128);
        builder.Property(t => t.CredentialReferenceId).HasMaxLength(256);

        // 变量字典存 jsonb；比较器保证 EF 正确检测字典内容变化。
        builder.Property(t => t.Variables)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.SequenceEqual(b)),
                    a => a.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                    a => new Dictionary<string, string>(a)));

        // 领取扫描的高频谓词：按状态过滤。
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => new { t.Status, t.LeaseExpiresAt });
        builder.HasIndex(t => new { t.Status, t.ScheduledAt });
    }
}

public sealed class DeadLetterEntityConfiguration : IEntityTypeConfiguration<DeadLetterEntity>
{
    public void Configure(EntityTypeBuilder<DeadLetterEntity> builder)
    {
        builder.ToTable("dead_letters");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.TaskId).IsRequired();
        builder.HasIndex(d => d.TaskId).IsUnique();
        builder.Property(d => d.SourceId).HasMaxLength(128).IsRequired();
        builder.Property(d => d.Reason).HasMaxLength(2048).IsRequired();
    }
}

/// <summary>领域聚合 ↔ 持久化实体的双向映射。仓储之外不允许直接操作实体。</summary>
public static class CrawlerTaskMapper
{
    public static CrawlerTaskEntity ToEntity(CrawlerTask task) =>
        new()
        {
            Id = task.Id,
            SourceId = task.Payload.SourceId,
            Capability = (int)task.Payload.Capability,
            Variables = new Dictionary<string, string>(task.Payload.Variables),
            CredentialReferenceId = task.Payload.CredentialReferenceId,
            Status = (int)task.Status,
            AttemptCount = task.AttemptCount,
            MaxAttempts = task.MaxAttempts,
            ScheduledAt = task.ScheduledAt,
            LeaseOwner = task.LeaseOwner,
            LeaseExpiresAt = task.LeaseExpiresAt,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
        };

    public static CrawlerTask ToDomain(CrawlerTaskEntity entity) =>
        CrawlerTask.Rehydrate(
            entity.Id,
            new CrawlPayload(
                entity.SourceId,
                (SourceCapability)entity.Capability,
                new Dictionary<string, string>(entity.Variables),
                entity.CredentialReferenceId),
            (CrawlerTaskStatus)entity.Status,
            entity.AttemptCount,
            entity.MaxAttempts,
            entity.LeaseOwner,
            entity.LeaseExpiresAt,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ScheduledAt);

    /// <summary>把聚合当前状态写回已跟踪的实体（保持同一实例，供 EF 变更跟踪）。</summary>
    public static void ApplyDomain(CrawlerTask task, CrawlerTaskEntity entity)
    {
        var fresh = ToEntity(task);
        entity.Status = fresh.Status;
        entity.AttemptCount = fresh.AttemptCount;
        entity.MaxAttempts = fresh.MaxAttempts;
        entity.ScheduledAt = fresh.ScheduledAt;
        entity.LeaseOwner = fresh.LeaseOwner;
        entity.LeaseExpiresAt = fresh.LeaseExpiresAt;
        entity.UpdatedAt = fresh.UpdatedAt;
        entity.Variables = fresh.Variables;
        entity.CredentialReferenceId = fresh.CredentialReferenceId;
    }
}
