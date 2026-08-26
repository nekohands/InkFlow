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
    }
}
