using System.Security.Claims;
using System.Text;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Crawling.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class RepairEndpointTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid DeadLetterId = Guid.Parse("018f1b3a-9c0a-7b21-8a2e-0123456789ab");
    private static readonly Guid ReplayTaskId = Guid.Parse("018f1b3a-9c0a-7b22-8a2e-0123456789ab");
    private static readonly string ActorId = Guid.Parse("018f1b3a-9c0a-7b23-8a2e-0123456789ab").ToString();

    [TestMethod]
    public async Task Replay_Result_Writes_Command_Audit_With_Actor_Reason_And_Reference()
    {
        var audit = new InMemoryAuditSink();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-repair-1",
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };

        var result = RepairEndpointResults.Replay(
            new DeadLetterReplayResult(DeadLetterReplayStatus.Replayed, ReplayTaskId),
            DeadLetterId,
            ActorId,
            "upstream recovered",
            context,
            audit,
            new FixedClock(T0),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        var eventRecord = audit.Events.Single();
        Assert.AreEqual("crawler.dead_letter.replay", eventRecord.Action);
        Assert.AreEqual("authenticated", eventRecord.ActorType);
        Assert.AreEqual(ActorId, eventRecord.ActorId);
        Assert.AreEqual("upstream recovered", eventRecord.Reason);
        StringAssert.Contains(eventRecord.Reference!, $"dead-letter:{DeadLetterId}");
        StringAssert.Contains(eventRecord.Reference!, $"replay-task:{ReplayTaskId}");
        Assert.AreEqual(200, context.Response.StatusCode);
    }

    [TestMethod]
    public async Task Failed_Replay_Result_Uses_Conflict_Status_And_Still_Audits()
    {
        var audit = new InMemoryAuditSink();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();

        var result = RepairEndpointResults.Replay(
            new DeadLetterReplayResult(DeadLetterReplayStatus.OriginalTaskNotDeadLettered),
            DeadLetterId,
            ActorId,
            "state check",
            context,
            audit,
            new FixedClock(T0),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        Assert.AreEqual(409, context.Response.StatusCode);
        Assert.AreEqual(1, audit.Events.Count);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        StringAssert.Contains(body, "original_task_state_conflict");
    }

    private sealed class InMemoryAuditSink : IAuditEventSink
    {
        public List<AuditEvent> Events { get; } = [];

        public ValueTask AppendAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
