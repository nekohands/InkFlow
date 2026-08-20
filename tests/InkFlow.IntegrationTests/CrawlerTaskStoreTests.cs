using InkFlow.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class CrawlerTaskStoreTests
{
    [TestMethod]
    public async Task Task_is_leased_once_then_recovered_after_lease_expiry()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        await using var provider = BuildProvider(postgres.GetConnectionString());
        await provider.MigrateInkFlowAsync();

        var now = DateTimeOffset.UtcNow;
        Guid taskId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var store = new CrawlerTaskStore(scope.ServiceProvider.GetRequiredService<CrawlingDbContext>());
            var task = NewTask("lease-once", now);
            taskId = task.Id;
            await store.EnqueueAsync(task);
            var leased = await store.LeaseNextAsync("worker-a", now, TimeSpan.FromSeconds(30));
            Assert.IsNotNull(leased);
            Assert.AreEqual(taskId, leased.Id);
            Assert.AreEqual(1, leased.Attempt);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var store = new CrawlerTaskStore(scope.ServiceProvider.GetRequiredService<CrawlingDbContext>());
            Assert.IsNull(await store.LeaseNextAsync("worker-b", now.AddSeconds(5), TimeSpan.FromSeconds(30)));
            var recovered = await store.LeaseNextAsync("worker-b", now.AddSeconds(31), TimeSpan.FromSeconds(30));
            Assert.IsNotNull(recovered);
            Assert.AreEqual(taskId, recovered.Id);
            Assert.AreEqual(2, recovered.Attempt);
            Assert.AreEqual("worker-b", recovered.LeaseOwner);
        }
    }

    [TestMethod]
    public async Task Failed_task_enters_dead_letter_after_max_attempts()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        await using var provider = BuildProvider(postgres.GetConnectionString());
        await provider.MigrateInkFlowAsync();

        var now = DateTimeOffset.UtcNow;
        var task = NewTask("dead-letter", now);
        task.MaxAttempts = 1;

        await using var scope = provider.CreateAsyncScope();
        var store = new CrawlerTaskStore(scope.ServiceProvider.GetRequiredService<CrawlingDbContext>());
        await store.EnqueueAsync(task);
        var leased = await store.LeaseNextAsync("worker-a", now, TimeSpan.FromSeconds(30));
        Assert.IsNotNull(leased);
        await store.MarkFailedAsync(task.Id, "worker-a", now.AddSeconds(1), "parse failed", TimeSpan.FromSeconds(5));

        var persisted = await store.FindAsync(task.Id);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(CrawlerTaskStatuses.DeadLetter, persisted.Status);
        Assert.AreEqual("parse failed", persisted.LastError);
    }

    [TestMethod]
    public async Task Duplicate_idempotency_key_is_rejected_by_database()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        await using var provider = BuildProvider(postgres.GetConnectionString());
        await provider.MigrateInkFlowAsync();

        var now = DateTimeOffset.UtcNow;
        await using var scope = provider.CreateAsyncScope();
        var store = new CrawlerTaskStore(scope.ServiceProvider.GetRequiredService<CrawlingDbContext>());
        await store.EnqueueAsync(NewTask("same-key", now));

        await Assert.ThrowsAsync<DbUpdateException>(() => store.EnqueueAsync(NewTask("same-key", now)));
    }

    private static CrawlerTaskRecord NewTask(string key, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Type = "FetchChapter",
        Payload = "{\"chapterId\":\"fixture\"}",
        IdempotencyKey = key,
        Priority = 10,
        MaxAttempts = 3,
        ScheduledAtUtc = now,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    private static PostgreSqlContainer CreatePostgres() => new PostgreSqlBuilder("postgres:18")
        .WithDatabase("inkflow")
        .WithUsername("inkflow")
        .WithPassword("inkflow-test")
        .Build();

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddInkFlowPersistence(connectionString);
        return services.BuildServiceProvider();
    }
}
