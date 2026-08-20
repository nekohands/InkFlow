using Npgsql;
using Testcontainers.PostgreSql;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class PostgreSqlContainerTests
{
    [TestMethod]
    public async Task PostgreSql_18_is_reachable()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("inkflow_test")
            .WithUsername("inkflow")
            .WithPassword("inkflow-test-password")
            .Build();

        await postgres.StartAsync();

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.AreEqual(1, Convert.ToInt32(result));
    }
}
