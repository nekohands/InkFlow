using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>持久化跨宿主安全审计事实的 DbContext。</summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options)
    : ModuleDbContext(options, AuditSchema.Name)
{
    public DbSet<AuditEventEntity> Events => Set<AuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AuditEventEntityConfiguration());
    }
}
