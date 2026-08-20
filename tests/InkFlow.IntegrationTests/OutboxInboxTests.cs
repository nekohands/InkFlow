using InkFlow.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class OutboxInboxTests
{
    [TestMethod]
    public async Task Outbox_message_participates_in_module_transaction()
    {
        await using var postgres = CreatePostgres("inkflow_outbox_test");
        await postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddInkFlowPersistence(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateInkFlowAsync();

        await using (var scope = provider.CreateAsyncScope())
        {
            var library = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await using var transaction = await library.Database.BeginTransactionAsync();
            library.AddOutboxMessage("BookCreated", "{\"bookId\":\"rolled-back\"}", DateTimeOffset.UtcNow);
            await library.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.AreEqual(0L, await CountRows(postgres.GetConnectionString(), "outbox_messages"));

        await using (var scope = provider.CreateAsyncScope())
        {
            var library = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await using var transaction = await library.Database.BeginTransactionAsync();
            library.AddOutboxMessage("BookCreated", "{\"bookId\":\"committed\"}", DateTimeOffset.UtcNow);
            await library.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        Assert.AreEqual(1L, await CountRows(postgres.GetConnectionString(), "outbox_messages"));
    }

    [TestMethod]
    public async Task Inbox_rejects_duplicate_message_for_same_consumer()
    {
        await using var postgres = CreatePostgres("inkflow_inbox_test");
        await postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddInkFlowPersistence(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateInkFlowAsync();

        var messageId = Guid.CreateVersion7();
        const string consumer = "test-consumer";

        await using (var firstScope = provider.CreateAsyncScope())
        {
            var messaging = firstScope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            messaging.InboxMessages.Add(new InboxMessage(messageId, consumer, DateTimeOffset.UtcNow));
            await messaging.SaveChangesAsync();
        }

        await using var secondScope = provider.CreateAsyncScope();
        var duplicateContext = secondScope.ServiceProvider.GetRequiredService<MessagingDbContext>();
        duplicateContext.InboxMessages.Add(new InboxMessage(messageId, consumer, DateTimeOffset.UtcNow));

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
    }

    private static PostgreSqlContainer CreatePostgres(string database) =>
        new PostgreSqlBuilder("postgres:18")
            .WithDatabase(database)
            .WithUsername("inkflow")
            .WithPassword("inkflow-test-password")
            .Build();

    private static async Task<long> CountRows(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM messaging.{table}", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
