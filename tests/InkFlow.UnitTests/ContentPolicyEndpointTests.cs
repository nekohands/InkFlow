using System.Text;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ContentPolicyEndpointTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid BookId = Guid.Parse("0198f1b3-a0ca-7b21-8a2e-0123456789ab");
    private static readonly Guid DecisionId = Guid.Parse("0198f1b3-a0ca-7b22-8a2e-0123456789ab");
    private static readonly string ActorId = Guid.Parse("0198f1b3-a0ca-7b23-8a2e-0123456789ab").ToString();

    [TestMethod]
    public async Task Takedown_Result_Writes_Command_Audit_And_Response()
    {
        var audit = new InMemoryAuditSink();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-policy-1",
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();
        var decision = ContentPolicyDecision.Create(
            BookId,
            ContentPolicyAction.Takedown,
            ActorId,
            "版权方通知",
            T0,
            DecisionId);
        var result = ContentPolicyEndpointResults.Command(
            new ContentPolicyCommandResult(BookId, IsTakedown: true, Changed: true, decision),
            ContentPolicyAction.Takedown,
            ActorId,
            decision.Reason,
            context,
            audit,
            new FixedClock(T0),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        var eventRecord = audit.Events.Single();
        Assert.AreEqual("content.policy.takedown", eventRecord.Action);
        Assert.AreEqual(ActorId, eventRecord.ActorId);
        Assert.AreEqual("版权方通知", eventRecord.Reason);
        StringAssert.Contains(eventRecord.Reference!, $"canonical-book:{BookId}");
        StringAssert.Contains(eventRecord.Reference!, $"decision:{DecisionId}");
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        StringAssert.Contains(body, "\"applied\"");
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
