using System.Text;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceCredentialBindingEndpointTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 30, 11, 0, 0, TimeSpan.Zero);
    private const string ActorId = "0198f1b3-a0ca-7b23-8a2e-0123456789ab";

    [TestMethod]
    public async Task Set_Result_Writes_Audit_And_Returns_Reference_Only()
    {
        var audit = new InMemoryAuditSink();
        var context = CreateContext();
        var result = SourceCredentialBindingEndpointResults.Command(
            new SourceCredentialBindingOperationResult(
                SourceCredentialBindingResultStatus.Updated,
                "official-a",
                "platform-reader"),
            SourceCredentialBindingCommandAction.Set,
            ActorId,
            "rotate binding",
            context,
            audit,
            new FixedClock(T0),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        var eventRecord = audit.Events.Single();
        Assert.AreEqual("source.credential_binding.set", eventRecord.Action);
        Assert.AreEqual("success", eventRecord.Outcome);
        Assert.AreEqual(ActorId, eventRecord.ActorId);
        Assert.AreEqual("rotate binding", eventRecord.Reason);
        StringAssert.Contains(eventRecord.Reference!, "source:official-a");
        StringAssert.Contains(eventRecord.Reference!, "binding:updated");
        Assert.IsFalse(eventRecord.Reference!.Contains("platform-reader", StringComparison.Ordinal));
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);

        var body = await ReadBodyAsync(context);
        StringAssert.Contains(body, "\"status\":\"updated\"");
        StringAssert.Contains(body, "\"sourceId\":\"official-a\"");
        StringAssert.Contains(body, "\"credentialReferenceId\":\"platform-reader\"");
        Assert.IsFalse(body.Contains("secret-value", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Clear_Result_Uses_Clear_Audit_And_Leaves_Reference_Null()
    {
        var audit = new InMemoryAuditSink();
        var context = CreateContext();
        var result = SourceCredentialBindingEndpointResults.Command(
            new SourceCredentialBindingOperationResult(
                SourceCredentialBindingResultStatus.Cleared,
                "official-a",
                null),
            SourceCredentialBindingCommandAction.Clear,
            ActorId,
            "retire binding",
            context,
            audit,
            new FixedClock(T0),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        var eventRecord = audit.Events.Single();
        Assert.AreEqual("source.credential_binding.clear", eventRecord.Action);
        Assert.AreEqual("success", eventRecord.Outcome);
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);

        var body = await ReadBodyAsync(context);
        StringAssert.Contains(body, "\"status\":\"cleared\"");
        StringAssert.Contains(body, "\"credentialReferenceId\":null");
    }

    [TestMethod]
    public async Task Failure_Result_Is_Audited_And_Uses_Client_Error_Status()
    {
        var audit = new InMemoryAuditSink();
        var context = CreateContext();
        var result = SourceCredentialBindingEndpointResults.Command(
            new SourceCredentialBindingOperationResult(
                SourceCredentialBindingResultStatus.SourceNotFound,
                "missing-source",
                null),
            SourceCredentialBindingCommandAction.Set,
            ActorId,
            "configure source",
            context,
            audit,
            new FixedClock(T0),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        var eventRecord = audit.Events.Single();
        Assert.AreEqual("source.credential_binding.set", eventRecord.Action);
        Assert.AreEqual("client_error", eventRecord.Outcome);
        Assert.AreEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
        StringAssert.Contains(await ReadBodyAsync(context), "\"error\":\"source_not_found\"");
    }

    [TestMethod]
    public void Source_Id_And_Reason_Validation_Is_Bounded()
    {
        Assert.IsTrue(
            SourceCredentialBindingEndpointResults.TryNormalizeSourceId(
                " official-a ",
                out var sourceId));
        Assert.AreEqual("official-a", sourceId);
        Assert.IsFalse(
            SourceCredentialBindingEndpointResults.TryNormalizeSourceId(
                "official a",
                out _));
        Assert.IsFalse(
            SourceCredentialBindingEndpointResults.TryNormalizeSourceId(
                new string('x', 129),
                out _));

        Assert.IsTrue(
            SourceCredentialBindingEndpointResults.TryNormalizeReason(
                " rotate\r\nbinding ",
                out var reason));
        Assert.AreEqual("rotate  binding", reason);
        Assert.IsFalse(SourceCredentialBindingEndpointResults.TryNormalizeReason(" ", out _));
        Assert.IsFalse(
            SourceCredentialBindingEndpointResults.TryNormalizeReason(
                new string('x', SourceCredentialBindingEndpointResults.MaxReasonLength + 1),
                out _));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-source-credential-binding-1",
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
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
