using System.Text.Json;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

public sealed class SourcesDbContext(DbContextOptions<SourcesDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public static readonly string SchemaName = SourcesSchema.Name;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DbSet<SourceEntity> Sources => Set<SourceEntity>();
    public DbSet<SourceBookEntity> SourceBooks => Set<SourceBookEntity>();
    public DbSet<SourceChapterEntity> SourceChapters => Set<SourceChapterEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SourcesSchema.Name);

        modelBuilder.Entity<SourceEntity>(b =>
        {
            b.ToTable("sources");
            b.HasKey(s => s.Id);
            b.Property(s => s.Id).HasMaxLength(128).IsRequired();
            b.Property(s => s.DisplayName).HasMaxLength(256).IsRequired();
            b.Property(s => s.BaseUrl).HasMaxLength(1024).IsRequired();
            b.Property(s => s.RuleDslJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<SourceBookEntity>(b =>
        {
            b.ToTable("source_books");
            b.HasKey(x => x.Id);
            b.Property(x => x.SourceId).HasMaxLength(128).IsRequired();
            b.Property(x => x.ExternalBookId).HasMaxLength(512).IsRequired();
            b.Property(x => x.Title).HasMaxLength(512).IsRequired();
            b.Property(x => x.Author).HasMaxLength(256).IsRequired();
            // (来源, 外部书 ID) 唯一：同一来源同一本书只有一条来源视图。
            b.HasIndex(x => new { x.SourceId, x.ExternalBookId }).IsUnique();
        });

        modelBuilder.Entity<SourceChapterEntity>(b =>
        {
            b.ToTable("source_chapters");
            b.HasKey(x => x.Id);
            b.Property(x => x.ExternalChapterId).HasMaxLength(512).IsRequired();
            b.Property(x => x.Title).HasMaxLength(512).IsRequired();
            b.HasIndex(x => new { x.SourceBookId, x.ExternalChapterId }).IsUnique();
            b.HasIndex(x => new { x.SourceBookId, x.ChapterIndex }).IsUnique();
            b.HasOne<SourceBookEntity>()
                .WithMany()
                .HasForeignKey(x => x.SourceBookId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    internal static string SerializeRuleDsl(SourceRuleDsl dsl) => JsonSerializer.Serialize(dsl, JsonOptions);

    internal static SourceRuleDsl? DeserializeRuleDsl(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<SourceRuleDsl>(json, JsonOptions);
}

public sealed class EfSourceRepository(SourcesDbContext db) : ISourceRepository
{
    public async Task AddAsync(Source source, CancellationToken cancellationToken = default)
    {
        db.Sources.Add(ToEntity(source));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Sources.FindAsync([sourceId], cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task SaveAsync(Source source, CancellationToken cancellationToken = default)
    {
        var entity = await db.Sources.FindAsync([source.Id], cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException(
                         $"source '{source.Id}' does not exist; use AddAsync first.");

        ApplyDomain(source, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static SourceEntity ToEntity(Source source) => new()
    {
        Id = source.Id,
        DisplayName = source.DisplayName,
        BaseUrl = source.BaseUrl,
        RuleDslJson = source.RuleDsl is null ? null : SourcesDbContext.SerializeRuleDsl(source.RuleDsl),
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
    };

    internal static Source ToDomain(SourceEntity entity) =>
        Source.Rehydrate(
            entity.Id,
            entity.DisplayName,
            entity.BaseUrl,
            SourcesDbContext.DeserializeRuleDsl(entity.RuleDslJson),
            entity.CreatedAt,
            entity.UpdatedAt);

    internal static void ApplyDomain(Source source, SourceEntity entity)
    {
        var fresh = ToEntity(source);
        entity.DisplayName = fresh.DisplayName;
        entity.BaseUrl = fresh.BaseUrl;
        entity.RuleDslJson = fresh.RuleDslJson;
        entity.UpdatedAt = fresh.UpdatedAt;
    }
}

/// <summary>dotnet-ef 设计时工厂。</summary>
public sealed class SourcesDbContextFactory : IDesignTimeDbContextFactory<SourcesDbContext>
{
    public SourcesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SourcesDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new SourcesDbContext(options);
    }
}
