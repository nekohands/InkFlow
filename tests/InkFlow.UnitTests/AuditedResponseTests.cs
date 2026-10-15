using System.Security.Claims;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class AuditedResponseTests
{
    [TestMethod]
    public async Task Response_Waits_For_Audit_And_Forwards_Cancellation()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var sink = new AuditSink(completion.Task);
        var context = new DefaultHttpContext { RequestServices = services };
        var result = Revoke(context, sink, cancellation.Token);

        var execution = result.ExecuteAsync(context);
        Assert.IsFalse(execution.IsCompleted);
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.AreEqual(cancellation.Token, sink.Token);
        Assert.AreEqual(1, sink.Calls);

        completion.SetResult();
        await execution;
        Assert.AreEqual(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.AreEqual(1, sink.Calls);
    }

    [TestMethod]
    public async Task Audit_Failure_Or_Cancellation_Prevents_Response()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        foreach (var error in new Exception[] { new InvalidOperationException("audit unavailable"), new OperationCanceledException() })
        {
            var context = new DefaultHttpContext { RequestServices = services };
            var sink = new AuditSink(Task.FromException(error));
            Exception? observed = null;
            try
            {
                await Revoke(context, sink, CancellationToken.None).ExecuteAsync(context);
            }
            catch (Exception exception)
            {
                observed = exception;
            }

            Assert.AreSame(error, observed);
            Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.IsFalse(context.Response.HasStarted);
            Assert.AreEqual(1, sink.Calls);
        }
    }

    private static IResult Revoke(HttpContext context, IAuditEventSink sink, CancellationToken cancellation) =>
        LegadoTokenEndpointResults.Revoke(
            Guid.NewGuid(), new ClaimsPrincipal(), context, sink, TimeProvider.System, cancellation);

    private sealed class AuditSink(Task completion) : IAuditEventSink
    {
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }

        public ValueTask AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = cancellationToken;
            return new ValueTask(completion);
        }
    }
}
