using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : ModuleDbContext(options, ContentSchema.Name)
{
    public DbSet<ContentVersionEntity> Versions => Set<ContentVersionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContentVersionEntity>(b =>
        {
            b.ToTable("versions");
            b.HasKey(x => x.Id);
            b.Property(x => x.SourceId).HasMaxLength(128).IsRequired();
            b.Property(x => x.CanonicalHash).HasMaxLength(64).IsRequired();
            b.Property(x => x.CanonicalText).IsRequired();

            // 不变量 2:同一章节下规范化内容唯一。
            b.HasIndex(x => new { x.CanonicalChapterId, x.CanonicalHash }).IsUnique();
            // 当前版本查询路径。
            b.HasIndex(x => new { x.CanonicalChapterId, x.IsCurrent });
        });
    }

    internal static ContentVersion ToDomain(ContentVersionEntity e) =>
        ContentVersion.Rehydrate(
            e.Id, e.CanonicalBookId, e.CanonicalChapterId, e.SourceId,
            e.CanonicalHash, e.CanonicalText, e.ParagraphCount,
            e.QualityScore, e.IsCurrent, e.CreatedAt);
}

public sealed class EfContentVersionRepository(ContentDbContext db) : IContentVersionRepository
{
    public async Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default)
    {
        db.Versions.Add(new ContentVersionEntity
        {
            Id = version.Id,
            CanonicalBookId = version.CanonicalBookId,
            CanonicalChapterId = version.CanonicalChapterId,
            SourceId = version.SourceId,
            CanonicalHash = version.CanonicalHash,
            CanonicalText = version.CanonicalText,
            ParagraphCount = version.ParagraphCount,
            QualityScore = version.QualityScore,
            IsCurrent = version.IsCurrent,
            CreatedAt = version.CreatedAt,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ContentVersion?> FindByHashAsync(
        Guid canonicalChapterId, string canonicalHash, CancellationToken cancellationToken = default)
    {
        var entity = await db.Versions
            .SingleOrDefaultAsync(
                v => v.CanonicalChapterId == canonicalChapterId && v.CanonicalHash == canonicalHash,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ContentDbContext.ToDomain(entity);
    }

    public async Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
        Guid canonicalChapterId, CancellationToken cancellationToken = default)
    {
        var entities = await db.Versions
            .Where(v => v.CanonicalChapterId == canonicalChapterId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ContentDbContext.ToDomain).ToList();
    }

    public async Task<ContentVersion?> GetCurrentForChapterAsync(
        Guid canonicalChapterId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Versions
            .SingleOrDefaultAsync(
                v => v.CanonicalChapterId == canonicalChapterId && v.IsCurrent,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ContentDbContext.ToDomain(entity);
    }

    /// <summary>原子切换当前版本:先全部置 false,再指定版本置 true。</summary>
    public async Task SetCurrentAsync(
        Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
    {
        await db.Versions
            .Where(v => v.CanonicalChapterId == chapterId && v.IsCurrent)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsCurrent, false), cancellationToken)
            .ConfigureAwait(false);

        await db.Versions
            .Where(v => v.Id == versionId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsCurrent, true), cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>dotnet-ef 设计时工厂。</summary>
public sealed class ContentDbContextFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new ContentDbContext(options);
    }
}
