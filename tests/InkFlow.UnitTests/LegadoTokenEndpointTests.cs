using System.Security.Claims;
using System.Text.Json;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class LegadoTokenEndpointTests
{
    [TestMethod]
    public async Task Issue_Audit_Does_Not_Contain_The_Raw_Token()
    {
        const string rawToken = "lf_lgd_secret-token";
        var tokenId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", Guid.NewGuid().ToString())],
                "test")),
        };
        context.RequestServices = new ServiceCollection()
            .AddOptions()
            .AddLogging()
            .BuildServiceProvider();
        var sink = new RecordingAuditSink();
        var response = new LegadoTokenIssueResponse(
            tokenId,
            "Reading 3.0",
            "lf_lgd_secret",
            "read",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(90),
            rawToken,
            JsonSerializer.SerializeToElement(new
            {
                header = $"{{\"X-InkFlow-Legado-Token\":\"{rawToken}\"}}",
            }));

        var result = LegadoTokenEndpointResults.Issue(
            response,
            context,
            sink,
            TimeProvider.System,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        Assert.AreEqual(1, sink.Events.Count);
        var audit = sink.Events[0];
        Assert.IsFalse(audit.Reference!.Contains(rawToken, StringComparison.Ordinal));
        Assert.IsFalse(audit.Action.Contains(rawToken, StringComparison.Ordinal));
        Assert.IsFalse(audit.Resource.Contains(rawToken, StringComparison.Ordinal));
        Assert.AreEqual(StatusCodes.Status201Created, context.Response.StatusCode);
    }

    private sealed class RecordingAuditSink : IAuditEventSink
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
}
