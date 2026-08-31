using InkFlow.BuildingBlocks.Persistence;
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
        using var messaging = new MessagingDbContext(
            new DbContextOptionsBuilder<MessagingDbContext>()
                .UseNpgsql(_container!.GetConnectionString())
                .Options);
        messaging.Database.Migrate();
        return (db, new EfCrawlerTaskRepository(db, new EfTransactionalOutboxWriter()));
    }

    private static CrawlPayload Payload(
        string sourceId = "example-source",
        string bookId = "42") =>
        new(sourceId, SourceCapability.Toc, new Dictionary<string, string> { ["bookId"] = bookId });

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

        // crawler schema 的任务、运行和死信核心表必须存在
        var tables = await db.Database.SqlQuery<string>(
                $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'crawler'""")
            .ToListAsync().ConfigureAwait(false);

        CollectionAssert.AreEquivalent(new[] { "tasks", "runs", "dead_letters" }, tables.ToList());

        var deadLetterColumns = await db.Database.SqlQuery<string>(
                $"""SELECT column_name AS "Value" FROM information_schema.columns WHERE table_schema = 'crawler' AND table_name = 'dead_letters'""")
            .ToListAsync().ConfigureAwait(false);
        CollectionAssert.IsSubsetOf(
            new[] { "ReplayTaskId", "ReplayedAt", "ReplayRequestedBy", "ReplayReason" },
            deadLetterColumns.ToList());

        var runIndexes = await db.Database.SqlQuery<string>(
                $"""SELECT indexname AS "Value" FROM pg_indexes WHERE schemaname = 'crawler' AND tablename = 'runs'""")
            .ToListAsync().ConfigureAwait(false);
        CollectionAssert.Contains(runIndexes.ToList(), "UX_runs_active_source_book");
    }

    [TestMethod]
    public async Task Concurrent_Active_Collection_Run_Bootstrap_Allows_Only_One_Insert()
    {
        var sourceId = $"concurrent-run-{Guid.NewGuid():N}";
        var externalBookId = "book-42";
        var first = CreateContext();
        await using var firstDb = first.Db;
        var second = CreateContext();
        await using var secondDb = second.Db;
        var firstRuns = new EfCollectionRunRepository(firstDb, new EfTransactionalOutboxWriter());
        var secondRuns = new EfCollectionRunRepository(secondDb, new EfTransactionalOutboxWriter());
        var firstRun = CollectionRun.Create(
            sourceId,
            externalBookId,
            "https://example.com/book/book-42",
            T0);
        var secondRun = CollectionRun.Create(
            sourceId,
            externalBookId,
            "https://example.com/book/book-42",
            T0.AddSeconds(1));
        var firstTask = CrawlerTask.Create(
            new CrawlPayload(
                sourceId,
                SourceCapability.BookInfo,
                new Dictionary<string, string> { ["bookId"] = externalBookId },
                RunId: firstRun.Id),
            createdAt: T0);
        var secondTask = CrawlerTask.Create(
            new CrawlPayload(
                sourceId,
                SourceCapability.BookInfo,
                new Dictionary<string, string> { ["bookId"] = externalBookId },
                RunId: secondRun.Id),
            createdAt: T0.AddSeconds(1));

        var inserted = await Task.WhenAll(
            firstRuns.TryAddWithInitialTaskAsync(firstRun, firstTask),
            secondRuns.TryAddWithInitialTaskAsync(secondRun, secondTask)).ConfigureAwait(false);

        Assert.AreEqual(1, inserted.Count(value => value));
        await using var verifyDb = CreateContext().Db;
        var activeCount = await verifyDb.Runs
            .CountAsync(run => run.SourceId == sourceId && run.ExternalBookId == externalBookId)
            .ConfigureAwait(false);
        Assert.AreEqual(1, activeCount);
        var taskCount = await verifyDb.Tasks
            .CountAsync(task => task.SourceId == sourceId && task.RunId != null)
            .ConfigureAwait(false);
        Assert.AreEqual(1, taskCount);

        await using var messaging = new MessagingDbContext(
            new DbContextOptionsBuilder<MessagingDbContext>()
                .UseNpgsql(_container!.GetConnectionString())
                .Options);
        var outboxCount = await messaging.OutboxMessages
            .CountAsync(message =>
                message.MessageType == "crawler.task.created" &&
                (message.Id == firstTask.Id || message.Id == secondTask.Id))
            .ConfigureAwait(false);
        Assert.AreEqual(1, outboxCount);
    }

    [TestMethod]
    public async Task Initial_Run_And_Task_Are_Atomic_When_Task_Insert_Fails()
    {
        var sourceId = $"atomic-bootstrap-{Guid.NewGuid():N}";
        var seed = CreateContext();
        await using (seed.Db)
        {
            var existingTask = CrawlerTask.Create(Payload(sourceId, "existing"), createdAt: T0);
            // Deliberately seed only the task fact. The atomic bootstrap under
            // test must also enqueue its outbox row, so no event is created here.
            seed.Db.Tasks.Add(CrawlerTaskMapper.ToEntity(existingTask));
            await seed.Db.SaveChangesAsync().ConfigureAwait(false);

            var run = CollectionRun.Create(
                sourceId,
                "book-42",
                "https://example.com/book/book-42",
                T0.AddSeconds(1));
            var duplicateTask = CrawlerTask.Rehydrate(
                existingTask.Id,
                new CrawlPayload(
                    sourceId,
                    SourceCapability.BookInfo,
                    new Dictionary<string, string> { ["bookId"] = "book-42" },
                    RunId: run.Id),
                CrawlerTaskStatus.Pending,
                attemptCount: 0,
                maxAttempts: 3,
                leaseOwner: null,
                leaseExpiresAt: null,
                createdAt: T0.AddSeconds(1),
                updatedAt: T0.AddSeconds(1),
                scheduledAt: T0.AddSeconds(1));

            var attempt = CreateContext();
            await using (attempt.Db)
            {
                var repository = new EfCollectionRunRepository(
                    attempt.Db,
                    new EfTransactionalOutboxWriter());
                var failed = false;
                try
                {
                    await repository
                        .TryAddWithInitialTaskAsync(run, duplicateTask)
                        .ConfigureAwait(false);
                }
                catch (DbUpdateException)
                {
                    failed = true;
                }

                Assert.IsTrue(failed, "the duplicate task must fail inside the atomic insert");
            }

            await using var verifyDb = CreateContext().Db;
            Assert.IsFalse(
                await verifyDb.Runs.AnyAsync(candidate => candidate.Id == run.Id).ConfigureAwait(false),
                "a failed initial task insert must roll back the new run");
            Assert.IsFalse(
                await verifyDb.Tasks.AnyAsync(candidate => candidate.Id == duplicateTask.Id &&
                                                            candidate.RunId == run.Id)
                    .ConfigureAwait(false),
                "the failed task must not be associated with the new run");

            await using var messaging = new MessagingDbContext(
                new DbContextOptionsBuilder<MessagingDbContext>()
                    .UseNpgsql(_container!.GetConnectionString())
                    .Options);
            Assert.IsFalse(
                await messaging.OutboxMessages
                    .AnyAsync(message => message.Id == duplicateTask.Id)
                    .ConfigureAwait(false),
                "the outbox event must roll back with the task and run");
        }
    }

    [TestMethod]
    public async Task Concurrent_Toc_Dedupe_Gate_Allows_Only_One_Task_Insert()
    {
        var sourceId = $"concurrent-toc-{Guid.NewGuid():N}";
        var first = CreateContext();
        await using var firstDb = first.Db;
        var second = CreateContext();
        await using var secondDb = second.Db;
        var firstTask = CrawlerTask.Create(Payload(sourceId, "book-42"), createdAt: T0);
        var secondTask = CrawlerTask.Create(Payload(sourceId, "book-42"), createdAt: T0.AddSeconds(1));

        var inserted = await Task.WhenAll(
            first.Repo.TryAddIfNoConflictingTaskAsync(firstTask, "bookId", "book-42"),
            second.Repo.TryAddIfNoConflictingTaskAsync(secondTask, "bookId", "book-42"));

        Assert.AreEqual(1, inserted.Count(value => value));
        await using var verifyDb = CreateContext().Db;
        var taskCount = await verifyDb.Tasks
            .CountAsync(task => task.SourceId == sourceId &&
                                task.Capability == (int)SourceCapability.Toc)
            .ConfigureAwait(false);
        Assert.AreEqual(1, taskCount, "并发追更扫描不得为同一本书创建多个 TOC 任务");
    }

    [TestMethod]
    public async Task Concurrent_Content_Dedupe_Gate_Allows_Only_One_Task_Insert()
    {
        var sourceId = $"concurrent-content-{Guid.NewGuid():N}";
        var first = CreateContext();
        await using var firstDb = first.Db;
        var second = CreateContext();
        await using var secondDb = second.Db;
        var firstTask = CrawlerTask.Create(
            new CrawlPayload(
                sourceId,
                SourceCapability.Content,
                new Dictionary<string, string>
                {
                    ["bookId"] = "book-42",
                    ["chapterId"] = "chapter-7",
                }),
            createdAt: T0);
        var secondTask = CrawlerTask.Create(
            new CrawlPayload(
                sourceId,
                SourceCapability.Content,
                new Dictionary<string, string>
                {
                    ["bookId"] = "book-42",
                    ["chapterId"] = "chapter-7",
                }),
            createdAt: T0.AddSeconds(1));

        var inserted = await Task.WhenAll(
            first.Repo.TryAddIfNoConflictingTaskAsync(
                firstTask,
                "chapterId",
                "chapter-7"),
            second.Repo.TryAddIfNoConflictingTaskAsync(
                secondTask,
                "chapterId",
                "chapter-7"));

        Assert.AreEqual(1, inserted.Count(value => value));
        await using var verifyDb = CreateContext().Db;
        var taskCount = await verifyDb.Tasks
            .CountAsync(task => task.SourceId == sourceId &&
                                task.Capability == (int)SourceCapability.Content)
            .ConfigureAwait(false);
        Assert.AreEqual(1, taskCount, "并发正文联动不得为同一章节创建多个 Content 任务");
    }

    [TestMethod]
    public async Task Concurrent_Reconcile_Does_Not_Overwrite_Control_State()
    {
        var sourceId = $"concurrent-reconcile-{Guid.NewGuid():N}";
        var first = CreateContext();
        await using var firstDb = first.Db;
        var second = CreateContext();
        await using var secondDb = second.Db;
        var firstRuns = new EfCollectionRunRepository(firstDb, new EfTransactionalOutboxWriter());
        var secondRuns = new EfCollectionRunRepository(secondDb, new EfTransactionalOutboxWriter());
        var run = CollectionRun.Create(
            sourceId,
            "book-42",
            "https://example.com/book/book-42",
            T0);
        run.MarkWorkStarted(T0.AddSeconds(1));
        await firstRuns.AddAsync(run).ConfigureAwait(false);

        await Task.WhenAll(
            firstRuns.ReconcileAsync(run.Id, T0.AddSeconds(2)),
            secondRuns.ApplyControlAsync(run.Id, "pause", T0.AddSeconds(3)))
            .ConfigureAwait(false);

        await using var verifyDb = CreateContext().Db;
        var persisted = await verifyDb.Runs
            .SingleAsync(candidate => candidate.Id == run.Id)
            .ConfigureAwait(false);
        Assert.AreEqual(
            (int)CollectionRunStatus.Paused,
            persisted.Status,
            "a concurrent progress fold must preserve the durable control state");
    }

    [TestMethod]
    public async Task Concurrent_Run_Mutation_Does_Not_Overwrite_Control_State()
    {
        var sourceId = $"concurrent-mutation-{Guid.NewGuid():N}";
        var first = CreateContext();
        await using var firstDb = first.Db;
        var second = CreateContext();
        await using var secondDb = second.Db;
        var firstRuns = new EfCollectionRunRepository(firstDb, new EfTransactionalOutboxWriter());
        var secondRuns = new EfCollectionRunRepository(secondDb, new EfTransactionalOutboxWriter());
        var run = CollectionRun.Create(
            sourceId,
            "book-42",
            "https://example.com/book/book-42",
            T0);
        run.MarkWorkStarted(T0.AddSeconds(1));
        await firstRuns.AddAsync(run).ConfigureAwait(false);
        var canonicalBookId = Guid.NewGuid();

        await Task.WhenAll(
            firstRuns.MutateAsync(
                run.Id,
                candidate => candidate.SetCanonicalBook(canonicalBookId, T0.AddSeconds(2)),
                T0.AddSeconds(2)),
            secondRuns.ApplyControlAsync(run.Id, "pause", T0.AddSeconds(3)))
            .ConfigureAwait(false);

        await using var verifyDb = CreateContext().Db;
        var persisted = await verifyDb.Runs
            .SingleAsync(candidate => candidate.Id == run.Id)
            .ConfigureAwait(false);
        Assert.AreEqual((int)CollectionRunStatus.Paused, persisted.Status);
        Assert.AreEqual(canonicalBookId, persisted.CanonicalBookId);
    }

    [TestMethod]
    public async Task Lease_Rechecks_Parent_Run_After_Control_Transaction_Commits()
    {
        var sourceId = $"lease-control-race-{Guid.NewGuid():N}";
        var seed = CreateContext();
        await using var seedDb = seed.Db;
        var runRepository = new EfCollectionRunRepository(seedDb, new EfTransactionalOutboxWriter());
        var run = CollectionRun.Create(
            sourceId,
            "book-42",
            "https://example.com/book/book-42",
            T0);
        await runRepository.AddAsync(run).ConfigureAwait(false);

        var task = CrawlerTask.Create(
            new CrawlPayload(
                sourceId,
                SourceCapability.BookInfo,
                new Dictionary<string, string> { ["bookId"] = "book-42" },
                RunId: run.Id),
            createdAt: T0);
        await seed.Repo.AddAsync(task).ConfigureAwait(false);

        var control = CreateContext();
        await using var controlDb = control.Db;
        await using var controlTransaction = await controlDb.Database
            .BeginTransactionAsync()
            .ConfigureAwait(false);
        var committed = false;
        try
        {
            await controlDb.Database
                .ExecuteSqlInterpolatedAsync($"""
                    UPDATE "crawler"."runs"
                    SET "Status" = {(int)CollectionRunStatus.Paused}
                    WHERE "Id" = {run.Id}
                    """)
                .ConfigureAwait(false);

            var lease = CreateContext();
            await using var leaseDb = lease.Db;
            var leaseTask = lease.Repo.TryLeaseAsync(
                T0.AddMinutes(1),
                "worker-after-control",
                TimeSpan.FromMinutes(1));

            var completedBeforeControlCommit = await Task.WhenAny(
                    leaseTask,
                    Task.Delay(TimeSpan.FromMilliseconds(500)))
                .ConfigureAwait(false);
            Assert.AreNotSame(
                leaseTask,
                completedBeforeControlCommit,
                "a lease must wait for the parent run control transaction before it can commit");

            await controlTransaction.CommitAsync().ConfigureAwait(false);
            committed = true;

            var leased = await leaseTask
                .WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
            Assert.IsNull(leased, "a task must not be leased after its run is paused");
        }
        finally
        {
            if (!committed)
            {
                await controlTransaction.RollbackAsync().ConfigureAwait(false);
            }
        }

        await using var verifyDb = CreateContext().Db;
        var persistedTask = await verifyDb.Tasks
            .SingleAsync(candidate => candidate.Id == task.Id)
            .ConfigureAwait(false);
        Assert.AreEqual((int)CrawlerTaskStatus.Pending, persistedTask.Status);
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
    public async Task HasConflictingTask_Respects_Blocking_Statuses_Variables_And_Capability()
    {
        var (_, repo) = CreateContext();

        var pending = CrawlerTask.Create(
            new CrawlPayload("src-conflict", SourceCapability.Content,
                new Dictionary<string, string> { ["chapterId"] = "ch-active" }),
            createdAt: T0);
        await repo.AddAsync(pending).ConfigureAwait(false);

        var completed = CrawlerTask.Create(
            new CrawlPayload("src-conflict", SourceCapability.Content,
                new Dictionary<string, string> { ["chapterId"] = "ch-done" }),
            createdAt: T0);
        completed.Lease("w", T0, TimeSpan.FromMinutes(1));
        completed.MarkRunning(T0.AddSeconds(1));
        completed.Complete(T0.AddSeconds(2));
        await repo.AddAsync(completed).ConfigureAwait(false);

        var deadLettered = CrawlerTask.Create(
            new CrawlPayload("src-conflict", SourceCapability.Content,
                new Dictionary<string, string> { ["chapterId"] = "ch-dead" }),
            maxAttempts: 1,
            createdAt: T0);
        deadLettered.Lease("w", T0, TimeSpan.FromMinutes(1));
        deadLettered.MarkRunning(T0.AddSeconds(1));
        deadLettered.Fail(T0.AddSeconds(2)); // 尝试耗尽 → 死信
        await repo.AddAsync(deadLettered).ConfigureAwait(false);

        Assert.IsTrue(
            await repo.HasConflictingTaskAsync("src-conflict", SourceCapability.Content, "chapterId", "ch-active")
                .ConfigureAwait(false),
            "在途任务必须阻止重复入队");
        Assert.IsFalse(
            await repo.HasConflictingTaskAsync("src-conflict", SourceCapability.Content, "chapterId", "ch-done")
                .ConfigureAwait(false),
            "已完成的章节允许重新入队(如上游重新出现未抓取状态)");
        Assert.IsTrue(
            await repo.HasConflictingTaskAsync("src-conflict", SourceCapability.Content, "chapterId", "ch-dead")
                .ConfigureAwait(false),
            "死信任务必须阻止周期扫描反复复活");
        Assert.IsFalse(
            await repo.HasConflictingTaskAsync("src-conflict", SourceCapability.Toc, "chapterId", "ch-active")
                .ConfigureAwait(false),
            "不同能力之间互不冲突");
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

    [TestMethod]
    public async Task Dead_Letter_Replay_Creates_New_Pending_Task_And_Is_Idempotent()
    {
        var (_, repo) = CreateContext();
        var payload = Payload("dead-letter-replay-idempotency-source");
        var task = CrawlerTask.Create(payload, maxAttempts: 1, T0);
        await repo.AddAsync(task).ConfigureAwait(false);

        var loaded = (await repo.GetAsync(task.Id).ConfigureAwait(false))!;
        loaded.Lease("w", T0, TimeSpan.FromMinutes(1));
        loaded.MarkRunning(T0);
        loaded.Fail(T0.AddMinutes(1));
        await repo.SaveAsync(loaded).ConfigureAwait(false);

        var deadLetter = DeadLetterTask.From(loaded, "upstream 503", T0.AddMinutes(2));
        await repo.AddDeadLetterAsync(deadLetter).ConfigureAwait(false);

        var command = DeadLetterReplayCommand.Create(
            deadLetter.Id,
            requestedBy: "operator-1",
            replayReason: "upstream recovered");
        var replayed = await repo
            .ReplayDeadLetterAsync(command, T0.AddMinutes(3))
            .ConfigureAwait(false);

        Assert.AreEqual(DeadLetterReplayStatus.Replayed, replayed.Status);
        Assert.IsNotNull(replayed.ReplayTaskId);
        Assert.AreNotEqual(task.Id, replayed.ReplayTaskId);

        var original = await repo.GetAsync(task.Id).ConfigureAwait(false);
        var replayTask = await repo.GetAsync(replayed.ReplayTaskId!.Value).ConfigureAwait(false);
        Assert.AreEqual(CrawlerTaskStatus.DeadLettered, original!.Status);
        Assert.AreEqual(CrawlerTaskStatus.Pending, replayTask!.Status);
        Assert.AreEqual(0, replayTask.AttemptCount);
        Assert.AreEqual(task.Payload.SourceId, replayTask.Payload.SourceId);
        Assert.AreEqual(task.Payload.Variables["bookId"], replayTask.Payload.Variables["bookId"]);
        Assert.AreEqual(T0.AddMinutes(3), replayTask.ScheduledAt);

        var resolved = (await repo.ListDeadLettersAsync(10).ConfigureAwait(false))
            .Single(letter => letter.Id == deadLetter.Id);
        Assert.AreEqual(replayed.ReplayTaskId, resolved.ReplayTaskId);
        Assert.AreEqual(T0.AddMinutes(3), resolved.ReplayedAt);
        Assert.AreEqual("operator-1", resolved.ReplayRequestedBy);
        Assert.AreEqual("upstream recovered", resolved.ReplayReason);

        var repeated = await repo
            .ReplayDeadLetterAsync(command, T0.AddMinutes(4))
            .ConfigureAwait(false);
        Assert.AreEqual(DeadLetterReplayStatus.AlreadyReplayed, repeated.Status);
        Assert.AreEqual(replayed.ReplayTaskId, repeated.ReplayTaskId);

        replayTask.Lease("w", T0.AddMinutes(5), TimeSpan.FromMinutes(1));
        replayTask.MarkRunning(T0.AddMinutes(5));
        replayTask.Complete(T0.AddMinutes(6));
        await repo.SaveAsync(replayTask).ConfigureAwait(false);

        Assert.IsFalse(
            await repo.HasConflictingTaskAsync(
                    payload.SourceId,
                    payload.Capability,
                    "bookId",
                    payload.Variables["bookId"])
                .ConfigureAwait(false),
            "已解决的原死信和已完成的重放任务不应永久阻塞后续入队");
    }

    [TestMethod]
    public async Task Dead_Letter_Replay_Is_Atomic_Under_Concurrent_Requests()
    {
        var seed = CreateContext();
        await using var seedDb = seed.Db;
        var payload = Payload("dead-letter-replay-concurrency-source");
        var task = CrawlerTask.Create(payload, maxAttempts: 1, T0);
        await seed.Repo.AddAsync(task).ConfigureAwait(false);

        var loaded = (await seed.Repo.GetAsync(task.Id).ConfigureAwait(false))!;
        loaded.Lease("w", T0, TimeSpan.FromMinutes(1));
        loaded.MarkRunning(T0);
        loaded.Fail(T0.AddMinutes(1));
        await seed.Repo.SaveAsync(loaded).ConfigureAwait(false);
        var deadLetter = DeadLetterTask.From(loaded, "upstream 503", T0.AddMinutes(2));
        await seed.Repo.AddDeadLetterAsync(deadLetter).ConfigureAwait(false);

        var first = CreateContext();
        await using var firstDb = first.Db;
        var second = CreateContext();
        await using var secondDb = second.Db;
        var command = DeadLetterReplayCommand.Create(deadLetter.Id, "operator-a", "retry once");

        var results = await Task.WhenAll(
            first.Repo.ReplayDeadLetterAsync(command, T0.AddMinutes(3)),
            second.Repo.ReplayDeadLetterAsync(command, T0.AddMinutes(3)));

        Assert.AreEqual(1, results.Count(result => result.Status == DeadLetterReplayStatus.Replayed));
        Assert.AreEqual(1, results.Count(result => result.Status == DeadLetterReplayStatus.AlreadyReplayed));
        Assert.AreEqual(
            results[0].ReplayTaskId,
            results[1].ReplayTaskId,
            "并发请求必须返回同一个重放任务");

        var count = await firstDb.Tasks
            .CountAsync(candidate => candidate.Id != task.Id && candidate.SourceId == payload.SourceId)
            .ConfigureAwait(false);
        Assert.AreEqual(1, count, "并发重放不得为同一来源创建多个新任务");
    }
}
