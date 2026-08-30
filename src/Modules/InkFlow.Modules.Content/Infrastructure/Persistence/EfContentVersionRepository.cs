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
    public DbSet<ContentSelectionDecisionEntity> SelectionDecisions => Set<ContentSelectionDecisionEntity>();
    public DbSet<ContentPolicyDecisionEntity> PolicyDecisions => Set<ContentPolicyDecisionEntity>();

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
            b.Property(x => x.QualityAlgorithmVersion).HasMaxLength(64).IsRequired();
            b.Property(x => x.QualityEvidence).HasMaxLength(1024).IsRequired();

            // 不变量 2:同一章节下规范化内容唯一。
            b.HasIndex(x => new { x.CanonicalChapterId, x.CanonicalHash }).IsUnique();
            // 当前版本查询路径。
            b.HasIndex(x => new { x.CanonicalChapterId, x.IsCurrent });
        });

        modelBuilder.Entity<ContentSelectionDecisionEntity>(b =>
        {
            b.ToTable("selection_decisions");
            b.HasKey(x => x.Id);
            b.Property(x => x.AlgorithmVersion).HasMaxLength(64).IsRequired();
            b.Property(x => x.Evidence).HasMaxLength(ContentSelectionAlgorithm.MaxEvidenceLength).IsRequired();
            b.HasIndex(x => new { x.CanonicalChapterId, x.CreatedAt });
        });

        modelBuilder.Entity<ContentPolicyDecisionEntity>(b =>
        {
            b.ToTable("policy_decisions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).IsRequired();
            b.Property(x => x.ActorId)
                .HasMaxLength(ContentPolicyDecision.MaxActorIdLength)
                .IsRequired();
            b.Property(x => x.Reason)
                .HasMaxLength(ContentPolicyDecision.MaxReasonLength)
                .IsRequired();
            b.HasIndex(x => new { x.CanonicalBookId, x.CreatedAt, x.Id });
        });
    }

    internal static ContentVersion ToDomain(ContentVersionEntity e) =>
        ContentVersion.Rehydrate(
            e.Id, e.CanonicalBookId, e.CanonicalChapterId, e.SourceId,
            e.CanonicalHash, e.CanonicalText, e.ParagraphCount,
            e.QualityScore, e.IsCurrent, e.CreatedAt,
            e.QualityAlgorithmVersion, e.QualityEvidence);
}

public sealed class EfContentSelectionDecisionRepository(ContentDbContext db)
    : IContentSelectionDecisionRepository
{
    public async Task AddAsync(
        ContentSelectionDecision decision,
        CancellationToken cancellationToken = default)
    {
        db.SelectionDecisions.Add(new ContentSelectionDecisionEntity
        {
            Id = decision.Id,
            CanonicalChapterId = decision.CanonicalChapterId,
            SelectedVersionId = decision.SelectedVersionId,
            AlgorithmVersion = decision.AlgorithmVersion,
            Evidence = decision.Evidence,
            CreatedAt = decision.CreatedAt,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ContentSelectionDecision?> GetLatestAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.SelectionDecisions
            .Where(x => x.CanonicalChapterId == canonicalChapterId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : ContentSelectionDecision.Rehydrate(
                entity.Id,
                entity.CanonicalChapterId,
                entity.SelectedVersionId,
                entity.AlgorithmVersion,
                entity.Evidence,
                entity.CreatedAt);
    }
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
            QualityAlgorithmVersion = version.QualityAlgorithmVersion,
            QualityEvidence = version.QualityEvidence,
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

    public Task<Guid?> GetCurrentCanonicalBookIdAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default) =>
        db.Versions
            .Where(v => v.CanonicalChapterId == canonicalChapterId && v.IsCurrent)
            .Select(v => (Guid?)v.CanonicalBookId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>在同一事务内原子切换当前版本，并拒绝跨章节版本。</summary>
    public async Task SetCurrentAsync(
        Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var targetExists = await db.Versions
            .AnyAsync(
                v => v.Id == versionId && v.CanonicalChapterId == chapterId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetExists)
        {
            throw new InvalidOperationException(
                $"content version '{versionId}' does not belong to chapter '{chapterId}'.");
        }

        // 单条 UPDATE 同时把同章节其它版本置为 false，并将目标置为 true，
        // 避免两条语句之间暴露“没有当前版本”的中间状态。
        await db.Versions
            .Where(v => v.CanonicalChapterId == chapterId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.IsCurrent, v => v.Id == versionId),
                cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class EfContentPolicyRepository(ContentDbContext db) : IContentPolicyRepository
{
    public async Task<ContentPolicyDecision?> GetLatestAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PolicyDecisions
            .AsNoTracking()
            .Where(x => x.CanonicalBookId == canonicalBookId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<ContentPolicyDecision>> ListLatestAsync(
        bool takenDownOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = db.PolicyDecisions
            .AsNoTracking()
            .Where(decision => !db.PolicyDecisions.Any(other =>
                other.CanonicalBookId == decision.CanonicalBookId &&
                (other.CreatedAt > decision.CreatedAt ||
                 (other.CreatedAt == decision.CreatedAt && other.Id > decision.Id))));

        if (takenDownOnly)
        {
            query = query.Where(decision => decision.Action == ContentPolicyAction.Takedown);
        }

        var entities = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
    }

    public async Task AddAsync(
        ContentPolicyDecision decision,
        CancellationToken cancellationToken = default)
    {
        db.PolicyDecisions.Add(new ContentPolicyDecisionEntity
        {
            Id = decision.Id,
            CanonicalBookId = decision.CanonicalBookId,
            Action = decision.Action,
            ActorId = decision.ActorId,
            Reason = decision.Reason,
            CreatedAt = decision.CreatedAt,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ContentPolicyDecision ToDomain(ContentPolicyDecisionEntity entity) =>
        ContentPolicyDecision.Rehydrate(
            entity.Id,
            entity.CanonicalBookId,
            entity.Action,
            entity.ActorId,
            entity.Reason,
            entity.CreatedAt);
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
