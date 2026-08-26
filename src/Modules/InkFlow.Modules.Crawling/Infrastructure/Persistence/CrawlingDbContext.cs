using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

public sealed class CrawlingDbContext(DbContextOptions<CrawlingDbContext> options)
    : ModuleDbContext(options, CrawlingSchema.Name)
{
    public DbSet<CrawlerTaskEntity> Tasks => Set<CrawlerTaskEntity>();
    public DbSet<DeadLetterEntity> DeadLetters => Set<DeadLetterEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new CrawlerTaskEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeadLetterEntityConfiguration());
    }
}
