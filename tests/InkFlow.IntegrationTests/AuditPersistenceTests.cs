using DotNet.Testcontainers.Images;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 PostgreSQL 上的持久化审计集成测试：验证迁移、追加写入和数据库追加式约束。
/// 本机无 Docker 时由类初始化明确报告环境阻塞，不能伪造为通过。
/// </summary>
[TestClass]
public sealed class AuditPersistenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

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
    public async Task Audit_Event_Is_Persisted_And_Cannot_Be_Updated()
    {
        await using var db = CreateContext();
        var auditEvent = AuditEvent.Create(
            action: "POST",
            resource: "/api/v1/repair/dead-letters",
            outcome: "success",
            statusCode: 200,
            occurredAt: T0,
            actorType: "authenticated",
            actorId: "operator-1",
            reason: "retry after upstream recovery",
            traceId: "trace-1",
            reference: "dead-letter:42");

        var sink = new PersistentAuditEventSink(db);
        await sink.AppendAsync(auditEvent).ConfigureAwait(false);

        var stored = await db.Events
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == auditEvent.Id)
            .ConfigureAwait(false);
        Assert.AreEqual(auditEvent.Action, stored.Action);
        Assert.AreEqual(auditEvent.Resource, stored.Resource);
        Assert.AreEqual(auditEvent.ActorId, stored.ActorId);
        Assert.AreEqual(auditEvent.Reason, stored.Reason);
        Assert.AreEqual(auditEvent.Reference, stored.Reference);

        Exception? mutationException = null;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "audit"."events" SET "Outcome" = {"tampered"} WHERE "Id" = {auditEvent.Id}""")
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            mutationException = exception;
        }

        Assert.IsNotNull(mutationException);
        StringAssert.Contains(mutationException.ToString(), "append-only");

        db.ChangeTracker.Clear();
        var unchanged = await db.Events
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == auditEvent.Id)
            .ConfigureAwait(false);
        Assert.AreEqual("success", unchanged.Outcome);
    }

    [TestMethod]
    public async Task Audit_Reader_Uses_Stable_Bounded_Cursor_And_Exact_Filters()
    {
        await using var db = CreateContext();
        const string action = "test.audit.reader";
        const string actorId = "audit-reader-operator";
        var sink = new PersistentAuditEventSink(db);

        for (var index = 0; index < 3; index++)
        {
            await sink.AppendAsync(AuditEvent.Create(
                action,
                $"/test/audit/{index}",
                "success",
                200,
                T0.AddMinutes(index),
                actorType: "authenticated",
                actorId: actorId)).ConfigureAwait(false);
        }

        var reader = new EfAuditEventReader(db);
        var firstPage = await reader.QueryAsync(
            new AuditEventQuery(
                From: T0,
                To: T0.AddMinutes(2),
                Action: action,
                Outcome: "success",
                ActorId: actorId,
                Before: null,
                Limit: 2)).ConfigureAwait(false);

        Assert.AreEqual(2, firstPage.Events.Count);
        Assert.IsNotNull(firstPage.NextCursor);
        Assert.AreEqual("/test/audit/2", firstPage.Events[0].Resource);
        Assert.AreEqual("/test/audit/1", firstPage.Events[1].Resource);

        var secondPage = await reader.QueryAsync(
            new AuditEventQuery(
                From: T0,
                To: T0.AddMinutes(2),
                Action: action,
                Outcome: "success",
                ActorId: actorId,
                Before: firstPage.NextCursor,
                Limit: 2)).ConfigureAwait(false);

        Assert.AreEqual(1, secondPage.Events.Count);
        Assert.AreEqual("/test/audit/0", secondPage.Events[0].Resource);
        Assert.IsNull(secondPage.NextCursor);
        Assert.IsFalse(firstPage.Events.Select(row => row.Id).Contains(secondPage.Events[0].Id));
    }

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new AuditDbContext(options);
        db.Database.Migrate();
        return db;
    }
}
