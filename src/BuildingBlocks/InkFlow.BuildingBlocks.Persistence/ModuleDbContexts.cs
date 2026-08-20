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
    : ModuleDbContext(options, DatabaseSchemas.Library);

public sealed class SourcesDbContext(DbContextOptions<SourcesDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Sources);

public sealed class CrawlingDbContext(DbContextOptions<CrawlingDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Crawler);

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : ModuleDbContext(options, DatabaseSchemas.Content);

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
