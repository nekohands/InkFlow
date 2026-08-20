using InkFlow.BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class SchemaMigrationTests
{
    [TestMethod]
    public async Task Empty_database_can_be_migrated_to_all_module_schemas()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("inkflow_migration_test")
            .WithUsername("inkflow")
            .WithPassword("inkflow-test-password")
            .Build();

        await postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddInkFlowPersistence(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();

        await provider.MigrateInkFlowAsync();

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();

        foreach (var schema in DatabaseSchemas.All)
        {
            await using var command = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema)",
                connection);
            command.Parameters.AddWithValue("schema", schema);

            var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
            Assert.IsTrue(exists, $"Expected schema '{schema}' to exist after migration.");
        }
    }
}
