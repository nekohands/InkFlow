using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Operations.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Operations.Infrastructure.Persistence;

public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options)
    : ModuleDbContext(options, OperationsSchema.Name)
{
    public DbSet<OperationsAlertIncidentEntity> AlertIncidents => Set<OperationsAlertIncidentEntity>();

    public DbSet<OperationsAlertHistoryEntity> AlertHistory => Set<OperationsAlertHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OperationsAlertIncidentEntity>(builder =>
        {
            builder.ToTable("alert_incidents");
            builder.HasKey(entity => entity.Fingerprint);
            builder.Property(entity => entity.Fingerprint).HasMaxLength(64).IsRequired();
            builder.Property(entity => entity.Code)
                .HasMaxLength(OperationsAlertObservation.MaxCodeLength)
                .IsRequired();
            builder.Property(entity => entity.Severity)
                .HasMaxLength(OperationsAlertObservation.MaxSeverityLength)
                .IsRequired();
            builder.Property(entity => entity.ResourceType)
                .HasMaxLength(OperationsAlertObservation.MaxResourceTypeLength)
                .IsRequired();
            builder.Property(entity => entity.ResourceId)
                .HasMaxLength(OperationsAlertObservation.MaxResourceIdLength)
                .IsRequired();
            builder.Property(entity => entity.Status).HasMaxLength(16).IsRequired();
            builder.Property(entity => entity.OccurrenceCount).IsRequired();
            builder.HasIndex(entity => new { entity.Status, entity.LastTransitionAt });
        });

        modelBuilder.Entity<OperationsAlertHistoryEntity>(builder =>
        {
            builder.ToTable("alert_history");
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Fingerprint).HasMaxLength(64).IsRequired();
            builder.Property(entity => entity.Code)
                .HasMaxLength(OperationsAlertObservation.MaxCodeLength)
                .IsRequired();
            builder.Property(entity => entity.Severity)
                .HasMaxLength(OperationsAlertObservation.MaxSeverityLength)
                .IsRequired();
            builder.Property(entity => entity.ResourceType)
                .HasMaxLength(OperationsAlertObservation.MaxResourceTypeLength)
                .IsRequired();
            builder.Property(entity => entity.ResourceId)
                .HasMaxLength(OperationsAlertObservation.MaxResourceIdLength)
                .IsRequired();
            builder.Property(entity => entity.Transition).HasMaxLength(16).IsRequired();
            builder.Property(entity => entity.OccurrenceCount).IsRequired();
            builder.HasIndex(entity => new { entity.OccurredAt, entity.Id });
            builder.HasIndex(entity => new { entity.Fingerprint, entity.OccurredAt });
        });
    }
}

/// <summary>dotnet-ef 设计时工厂；迁移生成不依赖真实运行连接串。</summary>
public sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    public OperationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres")
            .Options;

        return new OperationsDbContext(options);
    }
}
