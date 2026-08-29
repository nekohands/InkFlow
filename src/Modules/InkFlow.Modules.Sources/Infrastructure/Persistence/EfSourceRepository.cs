using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

public sealed class SourcesDbContext(DbContextOptions<SourcesDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public static readonly string SchemaName = SourcesSchema.Name;

    public DbSet<SourceEntity> Sources => Set<SourceEntity>();
    public DbSet<SourceBookEntity> SourceBooks => Set<SourceBookEntity>();
    public DbSet<SourceChapterEntity> SourceChapters => Set<SourceChapterEntity>();
    public DbSet<FetchArtifactEntity> FetchArtifacts => Set<FetchArtifactEntity>();
    public DbSet<SourceCapabilityHealthEntity> CapabilityHealth => Set<SourceCapabilityHealthEntity>();

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
            b.Property(s => s.DefaultCredentialReferenceId).HasMaxLength(SourceCredentialReferenceRules.MaxLength);
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

        modelBuilder.Entity<FetchArtifactEntity>(b =>
        {
            b.ToTable("fetch_artifacts");
            b.HasKey(x => x.Id);
            b.Property(x => x.SourceId).HasMaxLength(128).IsRequired();
            b.Property(x => x.ExternalBookId).HasMaxLength(512).IsRequired();
            b.Property(x => x.ExternalChapterId).HasMaxLength(512).IsRequired();
            b.Property(x => x.RawHash).HasMaxLength(64).IsRequired();
            // "最新产物"查询路径：按来源章节倒序取第一条。
            b.HasIndex(x => new { x.SourceId, x.ExternalChapterId, x.FetchedAt });
        });

        modelBuilder.Entity<SourceCapabilityHealthEntity>(b =>
        {
            b.ToTable("capability_health");
            b.HasKey(x => new { x.SourceId, x.Capability });
            b.Property(x => x.SourceId).HasMaxLength(128).IsRequired();
            b.Property(x => x.Capability).HasConversion<int>().IsRequired();
            b.Property(x => x.Status).HasConversion<int>().IsRequired();
            b.Property(x => x.LastFailureReason).HasMaxLength(SourceHealthPolicy.MaxFailureReasonLength);
            b.Property(x => x.AlgorithmVersion).HasMaxLength(64).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();
            b.HasIndex(x => x.Status);
        });
    }

    internal static string SerializeRuleDsl(SourceRuleDsl dsl) => SourceRuleDslJson.Serialize(dsl);

    internal static SourceRuleDsl? DeserializeRuleDsl(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var result = SourceRuleDslJson.Parse(json);
        if (!result.IsSuccess || result.Document is null)
        {
            throw new InvalidOperationException(
                $"stored source rule DSL rejected: {string.Join(" | ", result.Errors)}");
        }

        return result.Document;
    }
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

    public async Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.Sources
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
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
        DefaultCredentialReferenceId = source.DefaultCredentialReferenceId,
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
            entity.UpdatedAt,
            entity.DefaultCredentialReferenceId);

    internal static void ApplyDomain(Source source, SourceEntity entity)
    {
        var fresh = ToEntity(source);
        entity.DisplayName = fresh.DisplayName;
        entity.BaseUrl = fresh.BaseUrl;
        entity.RuleDslJson = fresh.RuleDslJson;
        entity.DefaultCredentialReferenceId = fresh.DefaultCredentialReferenceId;
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

public sealed class EfFetchArtifactRepository(SourcesDbContext db) : IFetchArtifactRepository
{
    public async Task AddAsync(FetchArtifact artifact, CancellationToken cancellationToken = default)
    {
        db.FetchArtifacts.Add(new FetchArtifactEntity
        {
            Id = artifact.Id,
            SourceId = artifact.SourceId,
            ExternalBookId = artifact.ExternalBookId,
            ExternalChapterId = artifact.ExternalChapterId,
            RawHash = artifact.RawHash,
            BodyLength = artifact.BodyLength,
            FetchedAt = artifact.FetchedAt,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FetchArtifact?> GetLatestAsync(
        string sourceId, string externalChapterId, CancellationToken cancellationToken = default)
    {
        var entity = await db.FetchArtifacts
            .Where(a => a.SourceId == sourceId && a.ExternalChapterId == externalChapterId)
            .OrderByDescending(a => a.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new FetchArtifact(
                entity.Id, entity.SourceId, entity.ExternalBookId, entity.ExternalChapterId,
                entity.RawHash, entity.BodyLength, entity.FetchedAt);
    }

    public async Task<IReadOnlySet<string>> ListFetchedExternalChapterIdsAsync(
        string sourceId,
        IEnumerable<string> externalChapterIds,
        CancellationToken cancellationToken = default)
    {
        var ids = externalChapterIds.ToArray();
        if (ids.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var fetched = await db.FetchArtifacts
            .Where(a => a.SourceId == sourceId && ids.Contains(a.ExternalChapterId))
            .Select(a => a.ExternalChapterId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HashSet<string>(fetched, StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<string>> ListRecentlyFetchedExternalChapterIdsAsync(
        string sourceId,
        IEnumerable<string> externalChapterIds,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var ids = externalChapterIds.ToArray();
        if (ids.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var recent = await db.FetchArtifacts
            .Where(a => a.SourceId == sourceId &&
                        a.FetchedAt >= since &&
                        ids.Contains(a.ExternalChapterId))
            .Select(a => a.ExternalChapterId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HashSet<string>(recent, StringComparer.Ordinal);
    }
}
