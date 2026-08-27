using InkFlow.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : ModuleDbContext(options, IdentitySchema.Name)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RefreshSessionEntity> Sessions => Set<RefreshSessionEntity>();
    public DbSet<AccessTokenEntity> AccessTokens => Set<AccessTokenEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(b =>
        {
            b.ToTable("users");
            b.HasKey(user => user.Id);
            b.Property(user => user.Email).HasMaxLength(256).IsRequired();
            b.Property(user => user.NormalizedEmail).HasMaxLength(256).IsRequired();
            b.Property(user => user.PasswordHash).HasMaxLength(1024).IsRequired();
            b.Property(user => user.Role).IsRequired();
            b.Property(user => user.Status).IsRequired();
            b.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<RefreshSessionEntity>(b =>
        {
            b.ToTable("sessions");
            b.HasKey(session => session.Id);
            b.Property(session => session.RefreshTokenHash).HasMaxLength(128).IsRequired();
            b.HasIndex(session => session.RefreshTokenHash).IsUnique();
            b.HasIndex(session => new { session.UserId, session.ExpiresAt });
            b.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessTokenEntity>(b =>
        {
            b.ToTable("access_tokens");
            b.HasKey(token => token.Id);
            b.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            b.HasIndex(token => token.TokenHash).IsUnique();
            b.HasIndex(token => new { token.SessionId, token.ExpiresAt });
            b.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<RefreshSessionEntity>()
                .WithMany()
                .HasForeignKey(token => token.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>dotnet-ef 设计时工厂；迁移生成不依赖运行环境的真实连接串。</summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new IdentityDbContext(options);
    }
}
