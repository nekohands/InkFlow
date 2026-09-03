using System.Text.Json;
using InkFlow.Api;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.ContractTests;

[TestClass]
public sealed class CollectionRunContractTests
{
    [TestMethod]
    public void Collection_Run_View_Uses_Stable_Web_Json_And_Page_Cursor()
    {
        var value = new CollectionRunView(
            Guid.CreateVersion7(),
            "fixture",
            "book-1",
            "https://fixture.example/book-1",
            null,
            CollectionRunStatus.Failed,
            CollectionRunStage.Content,
            TotalTaskCount: 4,
            CompletedTaskCount: 2,
            FailedTaskCount: 1,
            PendingTaskCount: 1,
            InFlightTaskCount: 0,
            CancelledTaskCount: 0,
            RemainingTaskCount: 1,
            LastError: "collection failed",
            CreatedAt: DateTimeOffset.Parse("2026-09-03T00:00:00Z"),
            UpdatedAt: DateTimeOffset.Parse("2026-09-03T00:01:00Z"));
        var cursor = new CollectionRunCursor(value.UpdatedAt, value.Id);

        var json = JsonSerializer.Serialize(
            new
            {
                data = new[] { CollectionRunEndpoints.ToResponse(value) },
                nextCursor = CollectionRunEndpoints.EncodeCursor(cursor),
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var run = root.GetProperty("data")[0];
        Assert.AreEqual("failed", run.GetProperty("status").GetString());
        Assert.AreEqual("content", run.GetProperty("stage").GetString());
        Assert.AreEqual(1, run.GetProperty("failedTaskCount").GetInt32());
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("nextCursor").GetString()));
        Assert.IsFalse(json.Contains("Variables", StringComparison.OrdinalIgnoreCase));
    }
}
