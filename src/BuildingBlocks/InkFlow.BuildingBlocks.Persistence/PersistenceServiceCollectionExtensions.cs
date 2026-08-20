using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InkFlow.BuildingBlocks.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddInkFlowPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<SchemaDbContext>(options => Configure(options, connectionString, "public"));
        services.AddDbContext<IdentityDbContext>(options => Configure(options, connectionString, DatabaseSchemas.Identity));
        services.AddDbContext<LibraryDbContext>(options => Configure(options, connectionString, DatabaseSchemas.Library));
        services.AddDbContext<SourcesDbContext>(options => Configure(options, connectionString, DatabaseSchemas.Sources));
        services.AddDbContext<CrawlingDbContext>(options => Configure(options, connectionString, DatabaseSchemas.Crawler));
        services.AddDbContext<ContentDbContext>(options => Configure(options, connectionString, DatabaseSchemas.Content));
        services.AddDbContext<ReadingDbContext>(options => Configure(options, connectionString, DatabaseSchemas.Reading));
        services.AddDbContext<MessagingDbContext>(options => Configure(options, connectionString, DatabaseSchemas.Messaging));

        return services;
    }

    private static void Configure(DbContextOptionsBuilder options, string connectionString, string historySchema)
    {
        options.UseNpgsql(connectionString, npgsql => npgsql
            .MigrationsAssembly(typeof(PersistenceAssembly).Assembly.FullName)
            .MigrationsHistoryTable("__EFMigrationsHistory", historySchema));
    }
}

public static class DatabaseSchemas
{
    public const string Identity = "identity";
    public const string Library = "library";
    public const string Sources = "sources";
    public const string Crawler = "crawler";
    public const string Content = "content";
    public const string Reading = "reading";
    public const string Messaging = "messaging";

    public static IReadOnlyList<string> All { get; } =
    [
        Identity,
        Library,
        Sources,
        Crawler,
        Content,
        Reading,
        Messaging
    ];
}
