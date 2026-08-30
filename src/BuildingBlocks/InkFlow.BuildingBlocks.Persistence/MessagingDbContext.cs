using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>集中保存跨模块 Outbox/Inbox 事实的 DbContext。</summary>
public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options)
    : ModuleDbContext(options, MessagingSchema.Name)
{
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<InboxMessageEntity> InboxMessages => Set<InboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OutboxMessageEntity>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.MessageType).HasMaxLength(128).IsRequired();
            builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(message => message.PayloadHash).HasMaxLength(64).IsRequired();
            builder.Property(message => message.RawPayload).HasColumnType("text");
            builder.Property(message => message.TraceId).HasMaxLength(128);
            builder.Property(message => message.LockOwner).HasMaxLength(128);
            builder.Property(message => message.LastError).HasMaxLength(128);
            builder.HasIndex(message => new
            {
                message.ProcessedAt,
                message.AvailableAt,
                message.OccurredAt,
                message.Id,
            });
            builder.HasIndex(message => new { message.LockedUntil, message.ProcessedAt });
        });

        modelBuilder.Entity<InboxMessageEntity>(builder =>
        {
            builder.ToTable("inbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.MessageType).HasMaxLength(128).IsRequired();
            builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(message => message.PayloadHash).HasMaxLength(64).IsRequired();
            builder.Property(message => message.RawPayload).HasColumnType("text");
            builder.Property(message => message.TraceId).HasMaxLength(128);
            builder.Property(message => message.OccurredAt);
            builder.Property(message => message.AvailableAt);
            builder.Property(message => message.LockOwner).HasMaxLength(128);
            builder.Property(message => message.DeadLetteredAt);
            builder.Property(message => message.LastError).HasMaxLength(128);
            builder.HasIndex(message => new { message.ProcessedAt, message.ReceivedAt });
            builder.HasIndex(message => new { message.LockedUntil, message.ProcessedAt });
            builder.HasIndex(message => new
            {
                message.MessageType,
                message.ProcessedAt,
                message.DeadLetteredAt,
                message.AvailableAt,
                message.LockedUntil,
                message.ReceivedAt,
                message.Id,
            });
        });
    }
}

/// <summary>dotnet-ef 设计时工厂：迁移生成不依赖运行环境连接串。</summary>
public sealed class MessagingDbContextFactory : IDesignTimeDbContextFactory<MessagingDbContext>
{
    public MessagingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new MessagingDbContext(options);
    }
}
