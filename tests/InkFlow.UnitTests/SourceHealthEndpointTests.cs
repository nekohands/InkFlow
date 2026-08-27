using System.Text;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceHealthEndpointTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 16, 0, 0, TimeSpan.Zero);
    private static readonly string ActorId = Guid.Parse("0198f1b3-a0ca-7b23-8a2e-0123456789ab").ToString();

    [TestMethod]
    public async Task Disable_Result_Writes_Command_Audit_And_Response()
    {
        var health = SourceCapabilityHealth.Create("official-a", SourceCapability.Content, T0);
        health.Disable("maintenance", T0.AddMinutes(1));
        var audit = new InMemoryAuditSink();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-source-health-1",
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();

        var result = SourceHealthEndpointResults.Command(
            health,
            SourceHealthCommandAction.Disable,
            ActorId,
            "maintenance",
            context,
            audit,
            new FixedClock(T0.AddMinutes(1)),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        var eventRecord = audit.Events.Single();
        Assert.AreEqual("source.health.disable", eventRecord.Action);
        Assert.AreEqual(ActorId, eventRecord.ActorId);
        Assert.AreEqual("maintenance", eventRecord.Reason);
        StringAssert.Contains(eventRecord.Reference!, "source:official-a");
        StringAssert.Contains(eventRecord.Reference!, "capability:Content");
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        StringAssert.Contains(body, "\"action\":\"disable\"");
        StringAssert.Contains(body, "\"status\":\"Disabled\"");
        StringAssert.Contains(body, "\"isAvailable\":false");
    }

    [TestMethod]
    public void Capability_And_Reason_Validation_Is_Bounded()
    {
        Assert.IsTrue(SourceHealthEndpointResults.TryParseCapability("content", out var capability));
        Assert.AreEqual(SourceCapability.Content, capability);
        Assert.IsFalse(SourceHealthEndpointResults.TryParseCapability("unknown-capability", out _));

        Assert.IsTrue(SourceHealthEndpointResults.TryNormalizeReason(" maintenance\r\nwindow ", out var reason));
        Assert.AreEqual("maintenance  window", reason);
        Assert.IsFalse(SourceHealthEndpointResults.TryNormalizeReason(" ", out _));
        Assert.IsFalse(
            SourceHealthEndpointResults.TryNormalizeReason(
                new string('x', SourceHealthPolicy.MaxFailureReasonLength + 1),
                out _));
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
