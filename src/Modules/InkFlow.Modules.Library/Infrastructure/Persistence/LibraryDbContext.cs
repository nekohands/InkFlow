using InkFlow.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Library.Infrastructure.Persistence;

public static class LibrarySchema
{
    public const string Name = "library";
}

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options)
    : ModuleDbContext(options, LibrarySchema.Name)
{
    public DbSet<CanonicalBookEntity> Books => Set<CanonicalBookEntity>();
    public DbSet<CanonicalChapterEntity> Chapters => Set<CanonicalChapterEntity>();
    public DbSet<PrivateBookEntity> PrivateBooks => Set<PrivateBookEntity>();
    public DbSet<MatchCandidateEntity> MatchCandidates => Set<MatchCandidateEntity>();
    public DbSet<ChapterMappingEntity> ChapterMappings => Set<ChapterMappingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CanonicalBookEntity>(b =>
        {
            b.ToTable("books");
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(512).IsRequired();
            b.Property(x => x.Author).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<CanonicalChapterEntity>(b =>
        {
            b.ToTable("chapters");
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(512).IsRequired();
            b.Property(x => x.BookId).IsRequired();
            // 章节序号在书内唯一；追加式目录依赖此约束兜底。
            b.HasIndex(x => new { x.BookId, x.ChapterIndex }).IsUnique();
            b.HasOne<CanonicalBookEntity>()
                .WithMany()
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PrivateBookEntity>(b =>
        {
            b.ToTable("private_books");
            b.HasKey(x => new { x.UserId, x.Id });
            b.Property(x => x.Title).HasMaxLength(512).IsRequired();
            b.Property(x => x.Author).HasMaxLength(256);
            // 私有书目必须按所有者过滤；Id 本身不赋予跨用户访问权。
            b.HasIndex(x => new { x.UserId, x.CreatedAt, x.Id });
        });

        modelBuilder.Entity<MatchCandidateEntity>(b =>
        {
            b.ToTable("match_candidates");
            b.HasKey(x => x.Id);
            b.Property(x => x.SourceId).HasMaxLength(128).IsRequired();
            b.Property(x => x.ExternalBookId).HasMaxLength(512).IsRequired();
            b.Property(x => x.Status).IsRequired();
            // 不变量 1：同一来源书至多一条候选记录。
            b.HasIndex(x => new { x.SourceId, x.ExternalBookId }).IsUnique();
            b.HasIndex(x => x.CanonicalBookId);
        });

        modelBuilder.Entity<ChapterMappingEntity>(b =>
        {
            b.ToTable("chapter_mappings");
            b.HasKey(x => x.Id);
            b.Property(x => x.SourceId).HasMaxLength(128).IsRequired();
            b.Property(x => x.ExternalChapterId).HasMaxLength(512).IsRequired();
            b.Property(x => x.AlignmentAlgorithmVersion).HasMaxLength(64).IsRequired();
            b.Property(x => x.AlignmentEvidence).HasMaxLength(1024).IsRequired();
            // 不变量 1：同一来源章节至多一条映射。
            b.HasIndex(x => new { x.SourceId, x.ExternalChapterId }).IsUnique();
            b.HasIndex(x => x.CanonicalChapterId);
        });
    }
}
