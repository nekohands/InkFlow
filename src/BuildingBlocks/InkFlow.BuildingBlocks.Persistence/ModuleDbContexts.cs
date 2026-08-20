using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

public sealed class SchemaDbContext(DbContextOptions<SchemaDbContext> options) : DbContext(options);

public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(schema);
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Identity);

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Library);

public sealed class SourcesDbContext(DbContextOptions<SourcesDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Sources);

public sealed class CrawlingDbContext(DbContextOptions<CrawlingDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Crawler);

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Content);

public sealed class ReadingDbContext(DbContextOptions<ReadingDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Reading);

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Messaging);
