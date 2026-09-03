using System.Text;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceLifecycleEndpointTests
{
    [TestMethod]
    public async Task Disable_Result_Is_Audited_And_Does_Not_Expose_Secrets()
    {
        var source = Source.Create(
            "source-a",
            "来源 A",
            "https://source-a.example",
            DateTimeOffset.UtcNow);
        source.Disable(DateTimeOffset.UtcNow);
        var audit = new InMemoryAuditSink();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-source-lifecycle-1",
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();

        var result = SourceLifecycleEndpointResults.Command(
            source,
            SourceLifecycleCommandAction.Disable,
            "0198f1b3-a0ca-7b21-8a2e-0123456789ab",
            "maintenance",
            context,
            audit,
            new FixedClock(DateTimeOffset.UtcNow),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        Assert.AreEqual("source.disable", audit.Events.Single().Action);
        Assert.AreEqual("maintenance", audit.Events.Single().Reason);
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        StringAssert.Contains(body, "\"isEnabled\":false");
        Assert.IsFalse(body.Contains("token", StringComparison.OrdinalIgnoreCase));
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
