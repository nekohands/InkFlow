using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InkFlow.BuildingBlocks.Persistence;

public static class DatabaseMigrator
{
    public static async Task MigrateInkFlowAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var schemaContext = scope.ServiceProvider.GetRequiredService<SchemaDbContext>();
        await schemaContext.Database.MigrateAsync(cancellationToken);
    }
}
