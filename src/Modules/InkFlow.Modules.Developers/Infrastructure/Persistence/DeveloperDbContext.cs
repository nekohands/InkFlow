using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Developers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Developers.Infrastructure.Persistence;

public sealed class DeveloperDbContext(DbContextOptions<DeveloperDbContext> options)
    : ModuleDbContext(options, DevelopersSchema.Name)
{
    public DbSet<DeveloperApplicationEntity> Applications => Set<DeveloperApplicationEntity>();
    public DbSet<DeveloperApiKeyEntity> ApiKeys => Set<DeveloperApiKeyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DeveloperApplicationEntity>(b =>
        {
            b.ToTable("applications");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(DeveloperApplication.MaxNameLength).IsRequired();
            b.Property(x => x.Environment).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.UserId, x.CreatedAt });
            b.HasIndex(x => new { x.UserId, x.RevokedAt });
        });

        modelBuilder.Entity<DeveloperApiKeyEntity>(b =>
        {
            b.ToTable("api_keys");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(DeveloperApiKey.MaxNameLength).IsRequired();
            b.Property(x => x.Prefix).HasMaxLength(DeveloperApiKey.MaxPrefixLength).IsRequired();
            b.Property(x => x.SecretHash).HasMaxLength(DeveloperApiKey.MaxHashLength).IsRequired();
            b.Property(x => x.Scope).HasMaxLength(DeveloperApiKey.MaxScopeLength).IsRequired();
            b.Property(x => x.Environment).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.ExpiresAt).IsRequired();
            b.HasIndex(x => x.SecretHash).IsUnique();
            b.HasIndex(x => new { x.UserId, x.ApplicationId, x.CreatedAt });
            b.HasIndex(x => new { x.ApplicationId, x.RevokedAt });
            b.HasOne<DeveloperApplicationEntity>()
                .WithMany()
                .HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>dotnet-ef 设计时工厂；迁移生成不依赖运行环境的真实连接串。</summary>
public sealed class DeveloperDbContextFactory : IDesignTimeDbContextFactory<DeveloperDbContext>
{
    public DeveloperDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DeveloperDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new DeveloperDbContext(options);
    }
}
