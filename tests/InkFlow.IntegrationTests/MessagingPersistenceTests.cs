using DotNet.Testcontainers.Images;
using InkFlow.BuildingBlocks.Messaging;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 PostgreSQL 18 上验证 Transactional Outbox / Inbox：业务行与消息同事务、
/// Outbox lease/重试、Inbox 重复消费和消息身份篡改检测。
/// 本机无 Docker 时由类初始化明确报告环境阻塞，不能伪造为通过。
/// </summary>
[TestClass]
public sealed class MessagingPersistenceTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

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

    [TestMethod]
    public async Task Migration_Creates_Messaging_Tables_And_No_Pending_Migrations()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);

        await using var db = CreateMessagingDb();
        var pending = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
        Assert.IsFalse(pending.Any(), "应用 messaging 迁移后不应再有 pending migrations");

        var tables = await db.Database.SqlQuery<string>(
                $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'messaging'""")
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { "inbox_messages", "outbox_messages" },
            tables.ToList());
    }

    [TestMethod]
    public async Task Crawler_Task_And_Outbox_Message_Are_Committed_Together()
    {
        await MigrateAllRequiredSchemasAsync().ConfigureAwait(false);
        await using var crawling = CreateCrawlingDb();
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "official-a",
                SourceCapability.Content,
                new Dictionary<string, string>
                {
                    ["bookId"] = "private-book-id",
                    ["chapterId"] = "private-chapter-id",
                },
                "credential-reference"),
            createdAt: T0);

        await new EfCrawlerTaskRepository(crawling, new EfTransactionalOutboxWriter())
            .AddAsync(task)
            .ConfigureAwait(false);

        await using var messaging = CreateMessagingDb();
        var message = await messaging.OutboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == task.Id)
            .ConfigureAwait(false);

        Assert.AreEqual(CrawlerIntegrationMessages.TaskCreatedType, message.MessageType);
        Assert.AreEqual(0, message.AttemptCount);
        Assert.IsNull(message.ProcessedAt);
        StringAssert.Contains(message.Payload, task.Id.ToString());
        StringAssert.Contains(message.Payload, "official-a");
        Assert.IsFalse(message.Payload.Contains("private-book-id", StringComparison.Ordinal));
        Assert.IsFalse(message.Payload.Contains("private-chapter-id", StringComparison.Ordinal));
        Assert.IsFalse(message.Payload.Contains("credential-reference", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Business_Row_And_Outbox_Roll_Back_As_One_Transaction()
    {
        await MigrateAllRequiredSchemasAsync().ConfigureAwait(false);
        await using var crawling = CreateCrawlingDb();
        var task = CrawlerTask.Create(
            new CrawlPayload("rollback-source", SourceCapability.Search, new Dictionary<string, string>()),
            createdAt: T0);
        var message = CrawlerIntegrationMessages.TaskCreated(task);

        await using (var transaction = await crawling.Database
                         .BeginTransactionAsync()
                         .ConfigureAwait(false))
        {
            crawling.Tasks.Add(CrawlerTaskMapper.ToEntity(task));
            await crawling.SaveChangesAsync().ConfigureAwait(false);
            await new EfTransactionalOutboxWriter()
                .EnqueueAsync(crawling, message)
                .ConfigureAwait(false);
            await transaction.RollbackAsync().ConfigureAwait(false);
        }

        await using var verifyCrawling = CreateCrawlingDb();
        await using var verifyMessaging = CreateMessagingDb();
        Assert.IsFalse(await verifyCrawling.Tasks.AnyAsync(candidate => candidate.Id == task.Id));
        Assert.IsFalse(await verifyMessaging.OutboxMessages.AnyAsync(candidate => candidate.Id == message.Id));
    }

    [TestMethod]
    public async Task Outbox_Lease_Retry_And_Publish_Are_At_Least_Once()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var message = IntegrationMessage.Create(
            "test.outbox.delivery",
            "{\"value\":42}",
            T0,
            id: Guid.CreateVersion7());

        await using (var enqueueDb = CreateMessagingDb())
        {
            await new EfMessagingMessageStore(enqueueDb)
                .EnqueueAsync(message)
                .ConfigureAwait(false);
        }

        var firstClaim = await ClaimAsync(message, "dispatcher-a", T0).ConfigureAwait(false);
        Assert.AreEqual(1, firstClaim.Count);
        Assert.AreEqual(1, firstClaim[0].AttemptCount);

        var blockedClaim = await ClaimAsync(message, "dispatcher-b", T0.AddSeconds(1))
            .ConfigureAwait(false);
        Assert.AreEqual(0, blockedClaim.Count);

        await using (var failDb = CreateMessagingDb())
        {
            await new EfMessagingMessageStore(failDb)
                .MarkFailedAsync(
                    message.Id,
                    "dispatcher-a",
                    T0.AddSeconds(2),
                    T0.AddMinutes(1),
                    "temporary_transport_failure")
                .ConfigureAwait(false);
        }

        var secondClaim = await ClaimAsync(message, "dispatcher-b", T0.AddMinutes(1))
            .ConfigureAwait(false);
        Assert.AreEqual(1, secondClaim.Count);
        Assert.AreEqual(2, secondClaim[0].AttemptCount);

        await using (var publishDb = CreateMessagingDb())
        {
            await new EfMessagingMessageStore(publishDb)
                .MarkPublishedAsync(message.Id, "dispatcher-b", T0.AddMinutes(1).AddSeconds(1))
                .ConfigureAwait(false);
        }

        await using var verify = CreateMessagingDb();
        var stored = await verify.OutboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == message.Id)
            .ConfigureAwait(false);
        Assert.IsNotNull(stored.ProcessedAt);
        Assert.AreEqual(2, stored.AttemptCount);
        Assert.IsNull(stored.LockOwner);
        Assert.IsNull(stored.LockedUntil);
    }

    [TestMethod]
    public async Task Inbox_Claims_Once_And_Duplicate_Message_Is_Already_Processed()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var message = IntegrationMessage.Create(
            "test.inbox.delivery",
            "{\"value\":7}",
            T0,
            id: Guid.CreateVersion7());

        InboxClaimResult first;
        await using (var firstDb = CreateMessagingDb())
        {
            first = await new EfMessagingMessageStore(firstDb)
                .TryClaimAsync(message, "consumer-a", T0, TimeSpan.FromMinutes(2))
                .ConfigureAwait(false);
        }

        Assert.AreEqual(InboxClaimStatus.Claimed, first.Status);
        Assert.AreEqual(1, first.AttemptCount);

        await using (var sameOwnerDb = CreateMessagingDb())
        {
            var sameOwner = await new EfMessagingMessageStore(sameOwnerDb)
                .TryClaimAsync(message, "consumer-a", T0.AddSeconds(1), TimeSpan.FromMinutes(2))
                .ConfigureAwait(false);
            Assert.AreEqual(InboxClaimStatus.AlreadyInProgress, sameOwner.Status);
            Assert.AreEqual(1, sameOwner.AttemptCount);
        }

        await using (var processDb = CreateMessagingDb())
        {
            await new EfMessagingMessageStore(processDb)
                .MarkProcessedAsync(message.Id, "consumer-a", T0.AddSeconds(1))
                .ConfigureAwait(false);
        }

        await using var duplicateDb = CreateMessagingDb();
        var duplicate = await new EfMessagingMessageStore(duplicateDb)
            .TryClaimAsync(message, "consumer-b", T0.AddSeconds(2), TimeSpan.FromMinutes(2))
            .ConfigureAwait(false);
        Assert.AreEqual(InboxClaimStatus.AlreadyProcessed, duplicate.Status);
        Assert.AreEqual(1, duplicate.AttemptCount);
    }

    [TestMethod]
    public async Task Inbox_Rejects_A_Message_With_Reused_Id_But_Different_Identity()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var messageId = Guid.CreateVersion7();
        var original = IntegrationMessage.Create(
            "test.inbox.identity",
            "{\"value\":1}",
            T0,
            id: messageId);
        var tampered = IntegrationMessage.Create(
            "test.inbox.identity.tampered",
            "{\"value\":2}",
            T0,
            id: messageId);

        await using (var firstDb = CreateMessagingDb())
        {
            var claim = await new EfMessagingMessageStore(firstDb)
                .TryClaimAsync(original, "consumer-a", T0, TimeSpan.FromMinutes(2))
                .ConfigureAwait(false);
            Assert.AreEqual(InboxClaimStatus.Claimed, claim.Status);
        }

        await using var tamperedDb = CreateMessagingDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EfMessagingMessageStore(tamperedDb)
                    .TryClaimAsync(tampered, "consumer-b", T0, TimeSpan.FromMinutes(2)))
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Concurrent_Inbox_Claims_Allow_Only_One_Active_Consumer()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var message = IntegrationMessage.Create(
            "test.inbox.concurrent",
            "{\"value\":9}",
            T0,
            id: Guid.CreateVersion7());
        await using var firstDb = CreateMessagingDb();
        await using var secondDb = CreateMessagingDb();
        var firstStore = new EfMessagingMessageStore(firstDb);
        var secondStore = new EfMessagingMessageStore(secondDb);

        var claims = await Task.WhenAll(
                firstStore.TryClaimAsync(message, "consumer-a", T0, TimeSpan.FromMinutes(2)),
                secondStore.TryClaimAsync(message, "consumer-b", T0, TimeSpan.FromMinutes(2)))
            .ConfigureAwait(false);

        Assert.AreEqual(1, claims.Count(claim => claim.Status == InboxClaimStatus.Claimed));
        Assert.AreEqual(1, claims.Count(claim => claim.Status == InboxClaimStatus.AlreadyInProgress));
    }

    [TestMethod]
    public async Task Outbox_Dispatcher_Publishes_And_Acknowledges_Real_Record()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var message = IntegrationMessage.Create(
            "test.dispatcher.real",
            "{\"value\":11}",
            T0,
            id: Guid.CreateVersion7());

        await using (var enqueueDb = CreateMessagingDb())
        {
            await new EfMessagingMessageStore(enqueueDb)
                .EnqueueAsync(message)
                .ConfigureAwait(false);
        }

        var publisher = new RecordingPublisher();
        await using (var dispatchDb = CreateMessagingDb())
        {
            var dispatcher = new OutboxDispatcher(
                new EfMessagingMessageStore(dispatchDb),
                publisher,
                new FixedTimeProvider(T0),
                new OutboxDispatcherOptions
                {
                    Owner = "dispatcher-real",
                    LeaseDuration = TimeSpan.FromMinutes(2),
                    BatchSize = 10,
                    RetryPolicy = new ExponentialMessageRetryPolicy(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromMinutes(1)),
                });

            var result = await dispatcher.DispatchOnceAsync().ConfigureAwait(false);
            Assert.AreEqual(1, result.ClaimedCount);
            Assert.AreEqual(1, result.PublishedCount);
            Assert.AreEqual(0, result.FailedCount);
        }

        await using var verify = CreateMessagingDb();
        var stored = await verify.OutboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == message.Id)
            .ConfigureAwait(false);
        Assert.AreEqual(message.Id, publisher.Published.Single().Id);
        Assert.IsNotNull(stored.ProcessedAt);
        Assert.AreEqual(1, stored.AttemptCount);
        Assert.IsNull(stored.LockOwner);
    }

    [TestMethod]
    public async Task PostgreSql_Relay_Publishes_Outbox_To_Idempotent_Inbox()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var message = IntegrationMessage.Create(
            "test.relay.real",
            "{\"value\":14}",
            T0,
            traceId: "trace-relay-real",
            id: Guid.CreateVersion7());

        await using (var enqueueDb = CreateMessagingDb())
        {
            await new EfMessagingMessageStore(enqueueDb)
                .EnqueueAsync(message)
                .ConfigureAwait(false);
        }

        await using var relayDb = CreateMessagingDb();
        var store = new EfMessagingMessageStore(relayDb);
        var publisher = new PostgreSqlInboxMessagePublisher(
            store,
            new FixedTimeProvider(T0.AddSeconds(2)));
        var dispatcher = new OutboxDispatcher(
            store,
            publisher,
            new FixedTimeProvider(T0.AddSeconds(2)),
            new OutboxDispatcherOptions
            {
                Owner = "relay-real",
                LeaseDuration = TimeSpan.FromMinutes(2),
                BatchSize = 10,
            });

        var result = await dispatcher.DispatchOnceAsync().ConfigureAwait(false);

        Assert.AreEqual(1, result.ClaimedCount);
        Assert.AreEqual(1, result.PublishedCount);
        Assert.AreEqual(0, result.FailedCount);

        var duplicateRecord = new OutboxMessageRecord(
            message.Id,
            message.MessageType,
            message.OccurredAt,
            message.OccurredAt,
            message.Payload,
            message.PayloadHash,
            message.TraceId,
            1,
            null,
            null,
            T0.AddSeconds(2),
            null,
            message.Payload);
        await publisher.PublishAsync(duplicateRecord).ConfigureAwait(false);

        await using var verify = CreateMessagingDb();
        var storedInbox = await verify.InboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == message.Id)
            .ConfigureAwait(false);
        var storedOutbox = await verify.OutboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == message.Id)
            .ConfigureAwait(false);

        Assert.AreEqual(message.MessageType, storedInbox.MessageType);
        Assert.AreEqual(message.Payload, storedInbox.RawPayload);
        Assert.AreEqual(message.PayloadHash, storedInbox.PayloadHash);
        Assert.AreEqual(message.TraceId, storedInbox.TraceId);
        Assert.AreEqual(T0.AddSeconds(2), storedInbox.ReceivedAt);
        Assert.IsNull(storedInbox.ProcessedAt);
        Assert.IsNotNull(storedOutbox.ProcessedAt);
    }

    [TestMethod]
    public async Task Outbox_Dispatcher_Releases_Transport_Failure_For_Retry()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var message = IntegrationMessage.Create(
            "test.dispatcher.retry",
            "{\"value\":12}",
            T0,
            id: Guid.CreateVersion7());

        await using (var enqueueDb = CreateMessagingDb())
        {
            await new EfMessagingMessageStore(enqueueDb)
                .EnqueueAsync(message)
                .ConfigureAwait(false);
        }

        await using (var dispatchDb = CreateMessagingDb())
        {
            var dispatcher = new OutboxDispatcher(
                new EfMessagingMessageStore(dispatchDb),
                new RecordingPublisher { ThrowOnPublish = true },
                new FixedTimeProvider(T0),
                new OutboxDispatcherOptions
                {
                    Owner = "dispatcher-retry",
                    LeaseDuration = TimeSpan.FromMinutes(2),
                    BatchSize = 10,
                    RetryPolicy = new ExponentialMessageRetryPolicy(
                        TimeSpan.FromSeconds(4),
                        TimeSpan.FromMinutes(1)),
                });

            var result = await dispatcher.DispatchOnceAsync().ConfigureAwait(false);
            Assert.AreEqual(1, result.ClaimedCount);
            Assert.AreEqual(0, result.PublishedCount);
            Assert.AreEqual(1, result.FailedCount);
        }

        await using var verify = CreateMessagingDb();
        var stored = await verify.OutboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == message.Id)
            .ConfigureAwait(false);
        Assert.AreEqual(T0.AddSeconds(4), stored.AvailableAt);
        Assert.AreEqual(MessageFailureCodes.PublishFailed, stored.LastError);
        Assert.IsNull(stored.ProcessedAt);
        Assert.IsNull(stored.LockOwner);
    }

    [TestMethod]
    public async Task Inbox_Consumer_Processes_And_Deduplicates_Real_Record()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var message = IntegrationMessage.Create(
            "test.consumer.real",
            "{\"value\":13}",
            T0,
            id: Guid.CreateVersion7());
        var handler = new RecordingHandler(message.MessageType);
        var resolver = new IntegrationMessageHandlerRegistry([handler]);

        InboxConsumeResult first;
        await using (var firstDb = CreateMessagingDb())
        {
            first = await new IntegrationMessageConsumer(
                    new EfMessagingMessageStore(firstDb),
                    resolver,
                    new FixedTimeProvider(T0),
                    new InboxConsumerOptions
                    {
                        Owner = "consumer-real-a",
                        LeaseDuration = TimeSpan.FromMinutes(2),
                    })
                .ConsumeAsync(message)
                .ConfigureAwait(false);
        }

        InboxConsumeResult duplicate;
        await using (var duplicateDb = CreateMessagingDb())
        {
            duplicate = await new IntegrationMessageConsumer(
                    new EfMessagingMessageStore(duplicateDb),
                    resolver,
                    new FixedTimeProvider(T0.AddSeconds(1)),
                    new InboxConsumerOptions
                    {
                        Owner = "consumer-real-b",
                        LeaseDuration = TimeSpan.FromMinutes(2),
                    })
                .ConsumeAsync(message)
                .ConfigureAwait(false);
        }

        Assert.AreEqual(InboxConsumeStatus.Processed, first.Status);
        Assert.AreEqual(InboxConsumeStatus.AlreadyProcessed, duplicate.Status);
        Assert.AreEqual(1, handler.CallCount);

        await using var verify = CreateMessagingDb();
        var stored = await verify.InboxMessages
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == message.Id)
            .ConfigureAwait(false);
        Assert.IsNotNull(stored.ProcessedAt);
        Assert.AreEqual(1, stored.AttemptCount);
        Assert.IsNull(stored.LockOwner);
    }

    [TestMethod]
    public async Task Message_Retention_Deletes_Only_Expired_Processed_Records()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var oldAt = T0.AddDays(-60);
        var oldOutbox = IntegrationMessage.Create(
            "test.retention.old.outbox",
            "{\"value\":21}",
            oldAt,
            id: Guid.CreateVersion7());
        var pendingOutbox = IntegrationMessage.Create(
            "test.retention.pending.outbox",
            "{\"value\":22}",
            oldAt.AddMinutes(1),
            id: Guid.CreateVersion7());
        var oldInbox = IntegrationMessage.Create(
            "test.retention.old.inbox",
            "{\"value\":23}",
            oldAt,
            id: Guid.CreateVersion7());
        var pendingInbox = IntegrationMessage.Create(
            "test.retention.pending.inbox",
            "{\"value\":24}",
            oldAt.AddMinutes(1),
            id: Guid.CreateVersion7());
        var freshOutbox = IntegrationMessage.Create(
            "test.retention.fresh.outbox",
            "{\"value\":25}",
            T0.AddDays(-1),
            id: Guid.CreateVersion7());
        var freshInbox = IntegrationMessage.Create(
            "test.retention.fresh.inbox",
            "{\"value\":26}",
            T0.AddDays(-1),
            id: Guid.CreateVersion7());

        await using (var seedDb = CreateMessagingDb())
        {
            seedDb.OutboxMessages.AddRange(
                ToOutboxEntity(oldOutbox, oldAt.AddMinutes(1)),
                ToOutboxEntity(pendingOutbox, null),
                ToOutboxEntity(freshOutbox, T0.AddDays(-1)));
            seedDb.InboxMessages.AddRange(
                ToInboxEntity(oldInbox, oldAt.AddMinutes(1)),
                ToInboxEntity(pendingInbox, null),
                ToInboxEntity(freshInbox, T0.AddDays(-1)));
            await seedDb.SaveChangesAsync().ConfigureAwait(false);
        }

        MessageRetentionResult result;
        await using (var cleanupDb = CreateMessagingDb())
        {
            result = await new MessageRetentionService(
                    new EfMessagingMessageStore(cleanupDb),
                    new FixedTimeProvider(T0))
                .CleanupAsync(new MessageRetentionOptions
                {
                    OutboxRetentionDays = 30,
                    InboxRetentionDays = 30,
                    BatchSize = 100,
                    MaxBatchesPerRun = 1,
                })
                .ConfigureAwait(false);
        }

        Assert.AreEqual(1, result.OutboxDeletedCount);
        Assert.AreEqual(1, result.InboxDeletedCount);

        await using var verify = CreateMessagingDb();
        Assert.IsNull(await verify.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == oldOutbox.Id)
            .ConfigureAwait(false));
        Assert.IsNotNull(await verify.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == pendingOutbox.Id)
            .ConfigureAwait(false));
        Assert.IsNotNull(await verify.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == freshOutbox.Id)
            .ConfigureAwait(false));
        Assert.IsNull(await verify.InboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == oldInbox.Id)
            .ConfigureAwait(false));
        var pendingInboxRecord = await verify.InboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == pendingInbox.Id)
            .ConfigureAwait(false);
        Assert.AreEqual(MessageFailureCodes.HandlerFailed, pendingInboxRecord.LastError);
        Assert.IsNotNull(await verify.InboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == freshInbox.Id)
            .ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Message_Retention_Enforces_Per_Run_Batch_Bound()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        var oldAt = T0.AddDays(-90);
        var messages = Enumerable.Range(0, 3)
            .Select(index => IntegrationMessage.Create(
                $"test.retention.batch.{index}",
                $"{{\"value\":{index}}}",
                oldAt.AddMinutes(index),
                id: Guid.CreateVersion7()))
            .ToArray();

        await using (var seedDb = CreateMessagingDb())
        {
            seedDb.OutboxMessages.AddRange(messages.Select(message =>
                ToOutboxEntity(message, oldAt.AddMinutes(10))));
            await seedDb.SaveChangesAsync().ConfigureAwait(false);
        }

        MessageRetentionResult result;
        await using (var cleanupDb = CreateMessagingDb())
        {
            result = await new MessageRetentionService(
                    new EfMessagingMessageStore(cleanupDb),
                    new FixedTimeProvider(T0))
                .CleanupAsync(new MessageRetentionOptions
                {
                    OutboxRetentionDays = 30,
                    InboxRetentionDays = 30,
                    BatchSize = 2,
                    MaxBatchesPerRun = 1,
                })
                .ConfigureAwait(false);
        }

        Assert.AreEqual(2, result.OutboxDeletedCount);
        Assert.AreEqual(0, result.InboxDeletedCount);

        await using var verify = CreateMessagingDb();
        var messageIds = messages.Select(message => message.Id).ToArray();
        Assert.AreEqual(
            1,
            await verify.OutboxMessages
                .AsNoTracking()
                .CountAsync(message => messageIds.Contains(message.Id))
                .ConfigureAwait(false));
    }

    private static async Task MigrateAllRequiredSchemasAsync()
    {
        await MigrateMessagingAsync().ConfigureAwait(false);
        await using var crawling = CreateCrawlingDb();
        await crawling.Database.MigrateAsync().ConfigureAwait(false);
    }

    private static async Task MigrateMessagingAsync()
    {
        await using var messaging = CreateMessagingDb();
        await messaging.Database.MigrateAsync().ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<OutboxMessageRecord>> ClaimAsync(
        IntegrationMessage message,
        string owner,
        DateTimeOffset now)
    {
        await using var db = CreateMessagingDb();
        var records = await new EfMessagingMessageStore(db)
            .ClaimBatchAsync(owner, now, TimeSpan.FromSeconds(30), 100)
            .ConfigureAwait(false);
        return records.Where(record => record.Id == message.Id).ToList();
    }

    private static MessagingDbContext CreateMessagingDb() =>
        new(new DbContextOptionsBuilder<MessagingDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options);

    private static CrawlingDbContext CreateCrawlingDb() =>
        new(new DbContextOptionsBuilder<CrawlingDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options);

    private static OutboxMessageEntity ToOutboxEntity(
        IntegrationMessage message,
        DateTimeOffset? processedAt) =>
        new()
        {
            Id = message.Id,
            MessageType = message.MessageType,
            OccurredAt = message.OccurredAt,
            AvailableAt = message.OccurredAt,
            Payload = message.Payload,
            PayloadHash = message.PayloadHash,
            RawPayload = message.Payload,
            TraceId = message.TraceId,
            AttemptCount = 1,
            ProcessedAt = processedAt,
            LastError = processedAt is null ? MessageFailureCodes.HandlerFailed : null,
        };

    private static InboxMessageEntity ToInboxEntity(
        IntegrationMessage message,
        DateTimeOffset? processedAt) =>
        new()
        {
            Id = message.Id,
            MessageType = message.MessageType,
            Payload = message.Payload,
            PayloadHash = message.PayloadHash,
            RawPayload = message.Payload,
            TraceId = message.TraceId,
            ReceivedAt = message.OccurredAt,
            AttemptCount = 1,
            ProcessedAt = processedAt,
            LastError = processedAt is null ? MessageFailureCodes.HandlerFailed : null,
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingPublisher : IIntegrationMessagePublisher
    {
        public List<OutboxMessageRecord> Published { get; } = [];

        public bool ThrowOnPublish { get; init; }

        public Task PublishAsync(
            OutboxMessageRecord message,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException("transport failure details");
            }

            Published.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler(string messageType) : IIntegrationMessageHandler
    {
        public string MessageType { get; } = messageType;

        public int CallCount { get; private set; }

        public Task HandleAsync(
            IntegrationMessage message,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
