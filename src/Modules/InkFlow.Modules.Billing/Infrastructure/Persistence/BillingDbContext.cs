using System.Text.Json;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Billing.Infrastructure.Persistence;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options)
    : ModuleDbContext(options, BillingSchema.Name)
{
    public DbSet<PlanEntity> Plans => Set<PlanEntity>();
    public DbSet<EntitlementAssignmentEntity> EntitlementAssignments => Set<EntitlementAssignmentEntity>();
    public DbSet<UsagePeriodEntity> UsagePeriods => Set<UsagePeriodEntity>();
    public DbSet<UsageLedgerEntity> UsageLedger => Set<UsageLedgerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlanEntity>(b =>
        {
            b.ToTable("plans");
            b.HasKey(x => new { x.Code, x.Version });
            b.Property(x => x.Code).HasMaxLength(PlanDefinition.MaxCodeLength).IsRequired();
            b.Property(x => x.Name).HasMaxLength(PlanDefinition.MaxNameLength).IsRequired();
            b.Property(x => x.MonthlyQuotaUnits).IsRequired();
            b.Property(x => x.QuotaAlgorithmVersion).HasMaxLength(PlanDefinition.MaxCodeLength).IsRequired();
            b.Property(x => x.EntitlementsJson).HasMaxLength(4096).IsRequired();

            foreach (var plan in BuiltInPlans.All)
            {
                b.HasData(new PlanEntity
                {
                    Code = plan.Code,
                    Version = plan.Version,
                    Name = plan.Name,
                    MonthlyQuotaUnits = plan.MonthlyQuotaUnits,
                    QuotaAlgorithmVersion = plan.QuotaAlgorithmVersion,
                    EntitlementsJson = JsonSerializer.Serialize(plan.Entitlements),
                });
            }
        });

        modelBuilder.Entity<EntitlementAssignmentEntity>(b =>
        {
            b.ToTable("entitlement_assignments");
            b.HasKey(x => x.Id);
            b.Property(x => x.PlanCode).HasMaxLength(PlanDefinition.MaxCodeLength).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(EntitlementAssignment.MaxReasonLength).IsRequired();
            b.HasIndex(x => new { x.UserId, x.CreatedAt, x.Id });
            b.HasOne<PlanEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.PlanCode, x.PlanVersion })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UsagePeriodEntity>(b =>
        {
            b.ToTable("usage_periods");
            b.HasKey(x => new { x.UserId, x.PeriodStart });
            b.Property(x => x.UsedUnits).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<UsageLedgerEntity>(b =>
        {
            b.ToTable("usage_ledger");
            b.HasKey(x => x.Id);
            b.Property(x => x.Operation).HasMaxLength(UsageLedgerEntry.MaxOperationLength).IsRequired();
            b.Property(x => x.AlgorithmVersion)
                .HasMaxLength(UsageLedgerEntry.MaxAlgorithmVersionLength)
                .IsRequired();
            b.Property(x => x.TraceId).HasMaxLength(UsageLedgerEntry.MaxTraceIdLength).IsRequired();
            b.Property(x => x.Units).IsRequired();
            b.HasIndex(x => new { x.UserId, x.PeriodStart, x.OccurredAt });
            b.HasIndex(x => new { x.ApplicationId, x.ApiKeyId, x.PeriodStart });
        });
    }
}

/// <summary>dotnet-ef 设计时工厂；迁移生成不依赖运行环境的真实连接串。</summary>
public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new BillingDbContext(options);
    }
}
