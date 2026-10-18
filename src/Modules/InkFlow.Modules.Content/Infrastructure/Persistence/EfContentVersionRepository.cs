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
    public DbSet<BookPackageJobEntity> PackageJobs => Set<BookPackageJobEntity>();

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

        modelBuilder.Entity<BookPackageJobEntity>(b =>
        {
            b.ToTable("package_jobs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Format).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.LeaseOwner).HasMaxLength(128);
            b.Property(x => x.ArtifactFileName).HasMaxLength(256);
            b.Property(x => x.ArtifactSha256).HasMaxLength(64);
            b.Property(x => x.FailureReason).HasMaxLength(2048);
            b.HasIndex(x => new { x.Status, x.ScheduledAt, x.CreatedAt });
            b.HasIndex(x => new { x.Status, x.ExpiresAt });
            b.HasIndex(x => new { x.CanonicalBookId, x.CreatedAt });
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
    private readonly ContentVersionQueryStore queryStore = new(db);
    private readonly ContentVersionCurrentStore currentStore = new(db);

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
        => await queryStore.FindByHashAsync(canonicalChapterId, canonicalHash, cancellationToken)
            .ConfigureAwait(false);

    public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
        Guid canonicalChapterId, CancellationToken cancellationToken = default)
        => queryStore.ListForChapterAsync(canonicalChapterId, cancellationToken);

    public Task<ContentVersion?> GetCurrentForChapterAsync(
        Guid canonicalChapterId, CancellationToken cancellationToken = default)
        => queryStore.GetCurrentForChapterAsync(canonicalChapterId, cancellationToken);

    public Task<IReadOnlyList<ContentVersion>> ListCurrentForBookAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
        => queryStore.ListCurrentForBookAsync(canonicalBookId, cancellationToken);

    public Task<Guid?> GetCurrentCanonicalBookIdAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default) =>
        queryStore.GetCurrentCanonicalBookIdAsync(canonicalChapterId, cancellationToken);

    public Task SetCurrentAsync(
        Guid chapterId, Guid versionId, CancellationToken cancellationToken = default) =>
        currentStore.SetCurrentAsync(chapterId, versionId, cancellationToken);
}

public sealed class EfContentPolicyRepository(ContentDbContext db) : IContentPolicyRepository
{
    private readonly ContentPolicyQueryStore queryStore = new(db);

    public Task<ContentPolicyDecision?> GetLatestAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default) =>
        queryStore.GetLatestAsync(canonicalBookId, cancellationToken);

    public Task<IReadOnlyList<ContentPolicyDecision>> ListLatestForBooksAsync(
        IReadOnlyCollection<Guid> canonicalBookIds,
        CancellationToken cancellationToken = default) =>
        queryStore.ListLatestForBooksAsync(canonicalBookIds, cancellationToken);

    public Task<IReadOnlyList<ContentPolicyDecision>> ListLatestAsync(
        bool takenDownOnly,
        int limit,
        CancellationToken cancellationToken = default) =>
        queryStore.ListLatestAsync(takenDownOnly, limit, cancellationToken);

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
