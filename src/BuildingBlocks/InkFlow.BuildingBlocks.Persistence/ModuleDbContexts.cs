using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

public sealed class SchemaDbContext(DbContextOptions<SchemaDbContext> options) : DbContext(options);

public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(schema);
        MessagingModel.Configure(modelBuilder, excludeFromMigrations: true);
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Identity);

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Library)
{
    public DbSet<BookRecord> Books => Set<BookRecord>();
    public DbSet<ChapterRecord> Chapters => Set<ChapterRecord>();
    public DbSet<SourceBookMatchRecord> SourceBookMatches => Set<SourceBookMatchRecord>();
    public DbSet<ChapterMappingRecord> ChapterMappings => Set<ChapterMappingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        CatalogModel.ConfigureLibrary(modelBuilder);
    }
}

public sealed class SourcesDbContext(DbContextOptions<SourcesDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Sources)
{
    public DbSet<SourceRecord> Sources => Set<SourceRecord>();
    public DbSet<SourceRuleVersionRecord> RuleVersions => Set<SourceRuleVersionRecord>();
    public DbSet<SourceBookRecord> SourceBooks => Set<SourceBookRecord>();
    public DbSet<SourceChapterRecord> SourceChapters => Set<SourceChapterRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        CatalogModel.ConfigureSources(modelBuilder);
    }
}

public sealed class CrawlingDbContext(DbContextOptions<CrawlingDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Crawler)
{
    public DbSet<CrawlerTaskRecord> CrawlerTasks => Set<CrawlerTaskRecord>();
    public DbSet<FetchArtifactRecord> FetchArtifacts => Set<FetchArtifactRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var task = modelBuilder.Entity<CrawlerTaskRecord>();
        task.ToTable("tasks", DatabaseSchemas.Crawler);
        task.HasKey(value => value.Id).HasName("pk_crawler_tasks");
        task.Property(value => value.Id).HasColumnName("id");
        task.Property(value => value.Type).HasColumnName("type").HasMaxLength(128).IsRequired();
        task.Property(value => value.SourceId).HasColumnName("source_id");
        task.Property(value => value.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        task.Property(value => value.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(512).IsRequired();
        task.Property(value => value.Priority).HasColumnName("priority");
        task.Property(value => value.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        task.Property(value => value.Attempt).HasColumnName("attempt");
        task.Property(value => value.MaxAttempts).HasColumnName("max_attempts");
        task.Property(value => value.ScheduledAtUtc).HasColumnName("scheduled_at_utc");
        task.Property(value => value.LeaseUntilUtc).HasColumnName("lease_until_utc");
        task.Property(value => value.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(256);
        task.Property(value => value.CreatedAtUtc).HasColumnName("created_at_utc");
        task.Property(value => value.UpdatedAtUtc).HasColumnName("updated_at_utc");
        task.Property(value => value.LastError).HasColumnName("last_error").HasMaxLength(4000);
        task.Property(value => value.TraceId).HasColumnName("trace_id").HasMaxLength(128);
        task.HasIndex(value => value.IdempotencyKey).IsUnique().HasDatabaseName("ux_crawler_tasks_idempotency_key");
        task.HasIndex(value => new { value.Status, value.ScheduledAtUtc, value.Priority }).HasDatabaseName("ix_crawler_tasks_dispatch");
        task.HasIndex(value => value.LeaseUntilUtc).HasDatabaseName("ix_crawler_tasks_lease_until");

        CatalogModel.ConfigureFetchArtifacts(modelBuilder);
    }
}

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Content)
{
    public DbSet<ContentBlobRecord> ContentBlobs => Set<ContentBlobRecord>();
    public DbSet<ContentVersionRecord> ContentVersions => Set<ContentVersionRecord>();
    public DbSet<ChapterSelectionRecord> ChapterSelections => Set<ChapterSelectionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        CatalogModel.ConfigureContent(modelBuilder);
    }
}

public sealed class ReadingDbContext(DbContextOptions<ReadingDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Reading);

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseSchemas.Messaging);
        MessagingModel.Configure(modelBuilder, excludeFromMigrations: false);
        base.OnModelCreating(modelBuilder);
    }
}

internal static class MessagingModel
{
    public static void Configure(ModelBuilder modelBuilder, bool excludeFromMigrations)
    {
        var outbox = modelBuilder.Entity<OutboxMessage>();
        if (excludeFromMigrations)
        {
            outbox.ToTable("outbox_messages", DatabaseSchemas.Messaging, table => table.ExcludeFromMigrations());
        }
        else
        {
            outbox.ToTable("outbox_messages", DatabaseSchemas.Messaging);
        }

        outbox.HasKey(message => message.Id);
        outbox.Property(message => message.Type).HasMaxLength(512).IsRequired();
        outbox.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        outbox.Property(message => message.LastError).HasMaxLength(4000);
        outbox.HasIndex(message => new { message.ProcessedAtUtc, message.OccurredAtUtc });

        var inbox = modelBuilder.Entity<InboxMessage>();
        if (excludeFromMigrations)
        {
            inbox.ToTable("inbox_messages", DatabaseSchemas.Messaging, table => table.ExcludeFromMigrations());
        }
        else
        {
            inbox.ToTable("inbox_messages", DatabaseSchemas.Messaging);
        }

        inbox.HasKey(message => new { message.MessageId, message.Consumer });
        inbox.Property(message => message.Consumer).HasMaxLength(256).IsRequired();
        inbox.HasIndex(message => message.ProcessedAtUtc);
    }
}
