using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 PostgreSQL 18 上的仓储集成测试（Testcontainers）。
/// 验证：迁移在空库上成功、聚合 round-trip、可领取扫描与死信写入。
/// 本机无 Docker 时这些用例无法运行，完整验证依赖远端 CI。
/// </summary>
[TestClass]
public sealed class CrawlerTaskRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static PostgreSqlContainer? _container;

    [ClassInitialize]
    public static async Task StartContainerAsync(TestContext _)
    {
        _container = new PostgreSqlBuilder(new DockerImage("postgres:18-alpine"))
            .Build();
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

    private static (CrawlingDbContext Db, EfCrawlerTaskRepository Repo) CreateContext()
    {
        var options = new DbContextOptionsBuilder<CrawlingDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new CrawlingDbContext(options);
        db.Database.Migrate();
        return (db, new EfCrawlerTaskRepository(db));
    }

    private static CrawlPayload Payload() =>
        new("example-source", SourceCapability.Toc, new Dictionary<string, string> { ["bookId"] = "42" });

    [TestMethod]
    public async Task Migrations_Create_Crawler_Schema_On_Empty_Database()
    {
        var options = new DbContextOptionsBuilder<CrawlingDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        await using var db = new CrawlingDbContext(options);
        await db.Database.MigrateAsync().ConfigureAwait(false);

        var pending = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
        Assert.IsFalse(pending.Any(), "空库应用迁移后不应再有 pending migrations");

        // crawler schema 的两张核心表必须存在
        var tables = await db.Database.SqlQuery<string>(
                $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'crawler'""")
            .ToListAsync().ConfigureAwait(false);

        CollectionAssert.AreEquivalent(new[] { "tasks", "dead_letters" }, tables.ToList());
    }

    [TestMethod]
    public async Task Task_Roundtrip_Preserves_Aggregate_State()
    {
        var (_, repo) = CreateContext();
        var task = CrawlerTask.Create(Payload(), maxAttempts: 3, T0);
        task.Lease("worker-a", T0.AddSeconds(1), TimeSpan.FromMinutes(1));
        task.MarkRunning(T0.AddSeconds(2));

        await repo.AddAsync(task).ConfigureAwait(false);

        var loaded = await repo.GetAsync(task.Id).ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(CrawlerTaskStatus.Running, loaded.Status);
        Assert.AreEqual("example-source", loaded.Payload.SourceId);
        Assert.AreEqual(SourceCapability.Toc, loaded.Payload.Capability);
        Assert.AreEqual("42", loaded.Payload.Variables["bookId"]);
        Assert.AreEqual(1, loaded.AttemptCount);
        Assert.AreEqual("worker-a", loaded.LeaseOwner);
    }

    [TestMethod]
    public async Task Save_Persists_Status_Transitions()
    {
        var (_, repo) = CreateContext();
        var task = CrawlerTask.Create(Payload(), maxAttempts: 1, T0);
        await repo.AddAsync(task).ConfigureAwait(false);

        var loaded = await repo.GetAsync(task.Id).ConfigureAwait(false);
        loaded!.Lease("w", T0.AddMinutes(1), TimeSpan.FromMinutes(1));
        loaded.MarkRunning(T0.AddMinutes(2));
        loaded.Fail(T0.AddMinutes(3)); // maxAttempts=1 → 死信
        await repo.SaveAsync(loaded).ConfigureAwait(false);

        var reloaded = await repo.GetAsync(task.Id).ConfigureAwait(false);
        Assert.AreEqual(CrawlerTaskStatus.DeadLettered, reloaded!.Status);
    }

    [TestMethod]
    public async Task FindLeasable_Returns_Pending_And_Expired_Leased_Or_Running()
    {
        var (_, repo) = CreateContext();

        var pendingTask = CrawlerTask.Create(
            new CrawlPayload("src-pending", SourceCapability.Search, new Dictionary<string, string>()), createdAt: T0);
        await repo.AddAsync(pendingTask).ConfigureAwait(false);

        var leasedFresh = CrawlerTask.Create(
            new CrawlPayload("src-fresh", SourceCapability.Toc, new Dictionary<string, string>()), createdAt: T0);
        await repo.AddAsync(leasedFresh).ConfigureAwait(false);
        leasedFresh.Lease("w", T0, TimeSpan.FromHours(1));
        await repo.SaveAsync(leasedFresh).ConfigureAwait(false); // 租约仍有效

        var leasedExpired = CrawlerTask.Create(
            new CrawlPayload("src-expired", SourceCapability.Content, new Dictionary<string, string>()), createdAt: T0);
        await repo.AddAsync(leasedExpired).ConfigureAwait(false);
        leasedExpired.Lease("w", T0, TimeSpan.FromSeconds(30));
        await repo.SaveAsync(leasedExpired).ConfigureAwait(false); // 已过期

        var runningExpired = CrawlerTask.Create(
            new CrawlPayload("src-running-expired", SourceCapability.Toc, new Dictionary<string, string>()), createdAt: T0);
        await repo.AddAsync(runningExpired).ConfigureAwait(false);
        runningExpired.Lease("w", T0, TimeSpan.FromSeconds(30));
        runningExpired.MarkRunning(T0.AddSeconds(1));
        await repo.SaveAsync(runningExpired).ConfigureAwait(false); // Worker 崩溃后租约已过期

        var futureRetry = CrawlerTask.Create(
            new CrawlPayload("src-future-retry", SourceCapability.Content, new Dictionary<string, string>()),
            maxAttempts: 3,
            createdAt: T0.AddDays(-3));
        futureRetry.Lease("w", T0, TimeSpan.FromMinutes(1));
        futureRetry.MarkRunning(T0.AddSeconds(1));
        futureRetry.Fail(T0.AddSeconds(2), T0.AddHours(1));
        await repo.AddAsync(futureRetry).ConfigureAwait(false);

        var leasable = await repo.FindLeasableAsync(T0.AddMinutes(5), limit: 10).ConfigureAwait(false);
        var sources = leasable.Select(t => t.Payload.SourceId).OrderBy(s => s).ToList();

        Assert.IsTrue(sources.Contains("src-pending"));
        Assert.IsTrue(sources.Contains("src-expired"));
        Assert.IsTrue(sources.Contains("src-running-expired"));
        Assert.IsFalse(sources.Contains("src-fresh"));
        Assert.IsFalse(sources.Contains("src-future-retry"));
    }

    [TestMethod]
    public async Task Save_Persists_Retry_Schedule_And_Atomic_Claim_Skips_Future_Task()
    {
        var seed = CreateContext();
        await using var seedDb = seed.Db;
        var task = CrawlerTask.Create(
            new CrawlPayload("src-persisted-future-retry", SourceCapability.Content, new Dictionary<string, string>()),
            maxAttempts: 3,
            createdAt: T0.AddDays(-100));
        await seed.Repo.AddAsync(task).ConfigureAwait(false);

        var update = CreateContext();
        await using var updateDb = update.Db;
        var loaded = await update.Repo.GetAsync(task.Id).ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        var retryAt = T0.AddHours(1);
        loaded!.Lease("worker", T0, TimeSpan.FromMinutes(1));
        loaded.MarkRunning(T0.AddSeconds(1));
        loaded.Fail(T0.AddSeconds(2), retryAt);
        await update.Repo.SaveAsync(loaded).ConfigureAwait(false);

        var claimant = CreateContext();
        await using var claimantDb = claimant.Db;
        var claim = await claimant.Repo
            .TryLeaseAsync(T0.AddMinutes(5), "other-worker", TimeSpan.FromMinutes(2))
            .ConfigureAwait(false);
        var reloaded = await claimant.Repo.GetAsync(task.Id).ConfigureAwait(false);

        Assert.IsNotNull(reloaded);
        Assert.AreEqual(CrawlerTaskStatus.Pending, reloaded!.Status);
        Assert.AreEqual(retryAt, reloaded.ScheduledAt);
        Assert.AreNotEqual(task.Id, claim?.Id, "未来调度任务不可被原子领取");
    }

    [TestMethod]
    public async Task TryLease_Atomically_Allows_Only_One_Worker_To_Claim_A_Task()
    {
        var seed = CreateContext();
        await using var seedDb = seed.Db;
        var task = CrawlerTask.Create(
            new CrawlPayload("src-concurrent", SourceCapability.Content, new Dictionary<string, string>()),
            createdAt: T0.AddDays(-1));
        await seed.Repo.AddAsync(task).ConfigureAwait(false);

        var first = CreateContext();
        await using var firstDb = first.Db;
        var second = CreateContext();
        await using var secondDb = second.Db;

        var claims = await Task.WhenAll(
            first.Repo.TryLeaseAsync(T0.AddMinutes(1), "worker-a", TimeSpan.FromMinutes(2)),
            second.Repo.TryLeaseAsync(T0.AddMinutes(1), "worker-b", TimeSpan.FromMinutes(2)));

        var targetClaims = claims
            .Where(candidate => candidate?.Id == task.Id)
            .Select(candidate => candidate!)
            .ToList();
        Assert.AreEqual(1, targetClaims.Count, "同一任务只能由一个并发 worker 领取");
        Assert.AreEqual(CrawlerTaskStatus.Leased, targetClaims[0].Status);
        Assert.AreEqual(1, targetClaims[0].AttemptCount);
        Assert.IsTrue(new[] { "worker-a", "worker-b" }.Contains(targetClaims[0].LeaseOwner));
    }

    [TestMethod]
    public async Task TryLease_Reclaims_An_Expired_Running_Task_And_Persists_New_Owner()
    {
        var seed = CreateContext();
        await using var seedDb = seed.Db;
        var task = CrawlerTask.Create(
            new CrawlPayload("src-running-reclaim", SourceCapability.Content, new Dictionary<string, string>()),
            maxAttempts: 3,
            createdAt: T0.AddDays(-2));
        task.Lease("worker-old", T0, TimeSpan.FromSeconds(30));
        task.MarkRunning(T0.AddSeconds(1));
        await seed.Repo.AddAsync(task).ConfigureAwait(false);

        var context = CreateContext();
        await using var db = context.Db;
        var claimed = await context.Repo
            .TryLeaseAsync(T0.AddMinutes(5), "worker-new", TimeSpan.FromMinutes(2))
            .ConfigureAwait(false);

        Assert.IsNotNull(claimed);
        Assert.AreEqual(CrawlerTaskStatus.Leased, claimed!.Status);
        Assert.AreEqual("worker-new", claimed.LeaseOwner);
        Assert.AreEqual(2, claimed.AttemptCount);

        var reloaded = await context.Repo.GetAsync(task.Id).ConfigureAwait(false);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(CrawlerTaskStatus.Leased, reloaded!.Status);
        Assert.AreEqual("worker-new", reloaded.LeaseOwner);
        Assert.AreEqual(2, reloaded.AttemptCount);
    }

    [TestMethod]
    public async Task Dead_Letter_Write_And_Read()
    {
        var (_, repo) = CreateContext();
        var task = CrawlerTask.Create(Payload(), maxAttempts: 1, T0);
        await repo.AddAsync(task).ConfigureAwait(false);

        var loaded = await repo.GetAsync(task.Id).ConfigureAwait(false);
        loaded!.Lease("w", T0, TimeSpan.FromMinutes(1));
        loaded.MarkRunning(T0);
        loaded.Fail(T0);
        var deadLetter = DeadLetterTask.From(loaded, "upstream 503", T0.AddMinutes(1));
        await repo.AddDeadLetterAsync(deadLetter).ConfigureAwait(false);

        var letters = await repo.ListDeadLettersAsync(limit: 10).ConfigureAwait(false);
        Assert.AreEqual(1, letters.Count);
        Assert.AreEqual(task.Id, letters[0].TaskId);
        Assert.AreEqual("upstream 503", letters[0].Reason);
    }
}
