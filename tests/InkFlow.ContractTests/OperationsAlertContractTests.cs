using System.Text.Json;
using InkFlow.Api;
using InkFlow.Modules.Operations.Application;

namespace InkFlow.ContractTests;

[TestClass]
public sealed class OperationsAlertContractTests
{
    [TestMethod]
    public void Operations_Alert_Snapshot_Uses_Stable_Web_Json_Names()
    {
        var snapshot = new OperationsAlertSnapshot(
            new DateTimeOffset(2026, 8, 28, 20, 0, 0, TimeSpan.Zero),
            "ready",
            1,
            1,
            false,
            [new OperationsAlert(
                "crawler_dead_letters_present",
                "critical",
                "crawler",
                "dead-letters",
                "dead-letter tasks are present; returned=1, hasMore=False, threshold=1")]);

        var json = JsonSerializer.Serialize(
            snapshot,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var alert = root.GetProperty("alerts")[0];

        Assert.IsTrue(root.TryGetProperty("generatedAt", out _));
        Assert.AreEqual("ready", root.GetProperty("status").GetString());
        Assert.AreEqual(1, root.GetProperty("totalAlertCount").GetInt32());
        Assert.AreEqual("crawler_dead_letters_present", alert.GetProperty("code").GetString());
        Assert.AreEqual("critical", alert.GetProperty("severity").GetString());
        Assert.AreEqual("dead-letters", alert.GetProperty("resourceId").GetString());
        Assert.IsFalse(json.Contains("Exception", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Operations_Alert_History_Uses_Stable_Web_Json_Names()
    {
        var entry = new OperationsAlertHistoryEntry(
            Guid.Parse("0198f1b3-a0c0-7b21-8a2e-0123456789ab"),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "crawler_dead_letters_present",
            "critical",
            "crawler",
            "dead-letters",
            OperationsAlertTransitions.Opened,
            new DateTimeOffset(2026, 8, 29, 14, 0, 0, TimeSpan.Zero),
            3);
        var response = OperationsAlertHistoryEndpointResults.ToResponse(
            new OperationsAlertHistoryPage(
                [entry],
                new OperationsAlertHistoryCursor(entry.OccurredAt, entry.Id)));

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var serializedEntry = root.GetProperty("entries")[0];

        Assert.IsTrue(root.GetProperty("nextCursor").GetString()!.Length > 0);
        Assert.AreEqual(entry.Id.ToString(), serializedEntry.GetProperty("id").GetString());
        Assert.AreEqual(entry.Fingerprint, serializedEntry.GetProperty("fingerprint").GetString());
        Assert.AreEqual("opened", serializedEntry.GetProperty("transition").GetString());
        Assert.AreEqual(3, serializedEntry.GetProperty("occurrenceCount").GetInt32());
        Assert.IsFalse(json.Contains("message", StringComparison.OrdinalIgnoreCase));
    }
}
