using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>Content Policy 持久化集成验证：迁移、最新决策派生和追加式触发器。</summary>
[TestClass]
public sealed class ContentPolicyRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);
    private static PostgreSqlContainer? _container;

    [ClassInitialize]
    public static async Task StartContainerAsync(TestContext _)
    {
        _container = new PostgreSqlBuilder(new DockerImage("postgres:18-alpine")).Build();
        await _container.StartAsync().ConfigureAwait(false);
    }

    [ClassCleanup]
    public static async Task StopContainerAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task Migration_Creates_Policy_Table_And_Leaves_No_Pending_Migrations()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);

        var pending = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
        Assert.IsFalse(pending.Any());

        var tables = await db.Database.SqlQuery<string>(
                $"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'content'")
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.Contains(tables, "policy_decisions");
    }

    [TestMethod]
    public async Task Latest_Decision_Roundtrips_And_History_Is_Append_Only()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var repository = new EfContentPolicyRepository(db);
        var bookId = Guid.CreateVersion7();
        var takedown = ContentPolicyDecision.Create(
            bookId,
            ContentPolicyAction.Takedown,
            "admin-1",
            "待核实授权",
            T0);
        var restore = ContentPolicyDecision.Create(
            bookId,
            ContentPolicyAction.Restore,
            "admin-1",
            "授权核验完成",
            T0.AddMinutes(1));

        await repository.AddAsync(takedown).ConfigureAwait(false);
        await repository.AddAsync(restore).ConfigureAwait(false);

        var latest = await repository.GetLatestAsync(bookId).ConfigureAwait(false);
        Assert.IsNotNull(latest);
        Assert.AreEqual(ContentPolicyAction.Restore, latest!.Action);
        Assert.AreEqual(restore.Id, latest.Id);

        await AssertAppendOnlyAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE content.policy_decisions SET \"Reason\" = 'tampered' WHERE \"Id\" = {restore.Id}"));
        await AssertAppendOnlyAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM content.policy_decisions WHERE \"Id\" = {restore.Id}"));

        var stillLatest = await repository.GetLatestAsync(bookId).ConfigureAwait(false);
        Assert.IsNotNull(stillLatest);
        Assert.AreEqual(restore.Id, stillLatest!.Id);
    }

    private static ContentDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        return new ContentDbContext(options);
    }

    private static async Task AssertAppendOnlyAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
            Assert.Fail("append-only mutation unexpectedly succeeded");
        }
        catch (PostgresException)
        {
            // 触发器应拒绝 UPDATE/DELETE。
        }
    }
}
