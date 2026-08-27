using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Reading.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Reading.Infrastructure.Persistence;

public sealed class ReadingDbContext(DbContextOptions<ReadingDbContext> options)
    : ModuleDbContext(options, ReadingSchema.Name)
{
    public DbSet<BookshelfEntryEntity> ShelfEntries => Set<BookshelfEntryEntity>();
    public DbSet<ReadingProgressEntity> Progress => Set<ReadingProgressEntity>();
    public DbSet<ReadingHistoryEntryEntity> History => Set<ReadingHistoryEntryEntity>();
    public DbSet<ReaderPreferenceEntity> Preferences => Set<ReaderPreferenceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BookshelfEntryEntity>(b =>
        {
            b.ToTable("shelf_entries");
            b.HasKey(x => new { x.UserId, x.CanonicalBookId });
            b.Property(x => x.Status).IsRequired();
            b.HasIndex(x => new { x.UserId, x.UpdatedAt });
        });

        modelBuilder.Entity<ReadingProgressEntity>(b =>
        {
            b.ToTable("progress");
            b.HasKey(x => new { x.UserId, x.CanonicalBookId });
            b.Property(x => x.CanonicalChapterId).IsRequired();
            b.Property(x => x.ParagraphIndex).IsRequired();
            b.Property(x => x.ProgressPercent).IsRequired();
            b.HasIndex(x => new { x.UserId, x.UpdatedAt });
        });

        modelBuilder.Entity<ReadingHistoryEntryEntity>(b =>
        {
            b.ToTable("history");
            b.HasKey(x => new { x.UserId, x.CanonicalBookId, x.CanonicalChapterId });
            b.HasIndex(x => new { x.UserId, x.LastReadAt });
        });

        modelBuilder.Entity<ReaderPreferenceEntity>(b =>
        {
            b.ToTable("preferences");
            b.HasKey(x => x.UserId);
            b.Property(x => x.FontSizePercent).IsRequired();
            b.Property(x => x.LineHeightPercent).IsRequired();
            b.Property(x => x.Theme).IsRequired();
        });
    }
}

/// <summary>dotnet-ef 设计时工厂；迁移生成不依赖运行环境的真实连接串。</summary>
public sealed class ReadingDbContextFactory : IDesignTimeDbContextFactory<ReadingDbContext>
{
    public ReadingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReadingDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new ReadingDbContext(options);
    }
}
