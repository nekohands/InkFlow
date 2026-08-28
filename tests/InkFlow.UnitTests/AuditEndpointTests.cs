using System.Text;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Persistence;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class AuditEndpointTests
{
    private static readonly Guid CursorId =
        Guid.Parse("018f1b3a-9c0a-7b21-8a2e-0123456789ab");

    [TestMethod]
    public void Query_Parser_Normalizes_Filters_And_Parses_Opaque_Cursor()
    {
        var cursor = EncodeCursor(
            new AuditEventCursor(
                new DateTimeOffset(2026, 8, 28, 14, 0, 0, TimeSpan.Zero),
                CursorId));

        var accepted = AuditEndpointResults.TryCreateQuery(
            "2026-08-28T00:00:00+08:00",
            "2026-08-29T00:00:00+08:00",
            " POST ",
            "success",
            " operator-1 ",
            cursor,
            10,
            out var query,
            out var error);

        Assert.IsTrue(accepted, error);
        Assert.IsNotNull(query);
        Assert.AreEqual(10, query.Limit);
        Assert.AreEqual("POST", query.Action);
        Assert.AreEqual("success", query.Outcome);
        Assert.AreEqual("operator-1", query.ActorId);
        Assert.AreEqual(CursorId, query.Before!.Id);
        Assert.AreEqual(
            new DateTimeOffset(2026, 8, 28, 14, 0, 0, TimeSpan.Zero),
            query.Before.OccurredAt);
    }

    [TestMethod]
    public void Query_Parser_Uses_Default_Limit()
    {
        var accepted = AuditEndpointResults.TryCreateQuery(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            out var query,
            out var error);

        Assert.IsTrue(accepted, error);
        Assert.AreEqual(AuditEndpointResults.DefaultLimit, query!.Limit);
    }

    [TestMethod]
    public void Query_Parser_Rejects_Unbounded_Or_Empty_Pages()
    {
        foreach (var limit in new[] { 0, 101 })
        {
            var accepted = AuditEndpointResults.TryCreateQuery(
                null,
                null,
                null,
                null,
                null,
                null,
                limit,
                out _,
                out var error);

            Assert.IsFalse(accepted);
            Assert.AreEqual("invalid_audit_query", error);
        }
    }

    [TestMethod]
    public void Query_Parser_Rejects_Reversed_Range_And_Malformed_Cursor()
    {
        var reversed = AuditEndpointResults.TryCreateQuery(
            "2026-08-29T00:00:00Z",
            "2026-08-28T00:00:00Z",
            null,
            null,
            null,
            null,
            null,
            out _,
            out _);
        var malformedCursor = AuditEndpointResults.TryCreateQuery(
            null,
            null,
            null,
            null,
            null,
            "not-a-cursor",
            null,
            out _,
            out _);

        Assert.IsFalse(reversed);
        Assert.IsFalse(malformedCursor);
    }

    [TestMethod]
    public void Response_Encodes_Next_Cursor_And_Maps_Safe_Fields()
    {
        var auditEvent = new InkFlow.BuildingBlocks.Security.AuditEvent
        {
            Id = CursorId,
            OccurredAt = new DateTimeOffset(2026, 8, 28, 14, 0, 0, TimeSpan.Zero),
            ActorType = "authenticated",
            ActorId = "operator-1",
            Action = "POST",
            Resource = "/api/v1/admin/audit/events",
            Outcome = "success",
            StatusCode = 200,
            Reason = "maintenance",
            TraceId = "trace-1",
            Reference = "book:1",
        };
        var response = AuditEndpointResults.ToResponse(
            new AuditEventPage(
                [auditEvent],
                new AuditEventCursor(auditEvent.OccurredAt, auditEvent.Id)));

        Assert.AreEqual(1, response.Events.Count);
        Assert.AreEqual(auditEvent.Resource, response.Events[0].Resource);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.NextCursor));
    }

    private static string EncodeCursor(AuditEventCursor cursor)
    {
        var payload = $"{cursor.OccurredAt:O}|{cursor.Id:D}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
