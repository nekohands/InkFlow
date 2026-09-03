using System.Text.Json;
using InkFlow.Api;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.AspNetCore.Http;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CollectionRunEndpointTests
{
    [TestMethod]
    public void ToResponse_Uses_CamelCase_Name_For_BookInfo_Stage()
    {
        var response = CollectionRunEndpoints.ToResponse(
            new CollectionRunView(
                Guid.NewGuid(),
                "fixture",
                "book-1",
                "https://fixture.example/book/book-1",
                null,
                CollectionRunStatus.Pending,
                CollectionRunStage.BookInfo,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.AreEqual("bookInfo", document.RootElement.GetProperty("stage").GetString());
    }

    [TestMethod]
    public void Progress_Percent_Does_Not_Count_Failed_Or_Cancelled_Tasks_As_Completed()
    {
        var view = new CollectionRunView(
            Guid.NewGuid(),
            "fixture",
            "book-1",
            "https://fixture.example/book/book-1",
            null,
            CollectionRunStatus.Failed,
            CollectionRunStage.Content,
            TotalTaskCount: 4,
            CompletedTaskCount: 1,
            FailedTaskCount: 2,
            PendingTaskCount: 0,
            InFlightTaskCount: 0,
            CancelledTaskCount: 1,
            RemainingTaskCount: 0,
            LastError: "one or more required collection tasks reached the dead-letter state.",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        Assert.AreEqual(25, view.ProgressPercent);
    }

    [TestMethod]
    public void Start_Status_Uses_Unprocessable_Entity_For_Resolver_Failures()
    {
        var statusCode = CollectionRunEndpoints.GetStartStatusCode(
            CollectionRunStartOutcome.Failure(
                "source-url.scheme",
                "only HTTP and HTTPS book URLs are supported."));

        Assert.AreEqual(StatusCodes.Status422UnprocessableEntity, statusCode);
    }

    [TestMethod]
    public void Start_Status_Uses_Bad_Request_For_Invalid_Input()
    {
        var statusCode = CollectionRunEndpoints.GetStartStatusCode(
            CollectionRunStartOutcome.Failure(
                "source-url.invalid",
                "book URL must be a valid absolute URL."));

        Assert.AreEqual(StatusCodes.Status400BadRequest, statusCode);
    }

    [TestMethod]
    public void List_Query_Uses_Bounded_Opaque_Cursor()
    {
        var id = Guid.Parse("0198f1b3-a0ca-7b21-8a2e-0123456789ab");
        var cursor = CollectionRunEndpoints.EncodeCursor(
            new CollectionRunCursor(
                new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero),
                id));

        var accepted = CollectionRunEndpoints.TryCreateQuery(
            limitRaw: 10,
            cursorRaw: cursor,
            out var limit,
            out var before,
            out var error);

        Assert.IsTrue(accepted, error);
        Assert.AreEqual(10, limit);
        Assert.AreEqual(id, before!.Id);

        Assert.IsFalse(CollectionRunEndpoints.TryCreateQuery(
            101, null, out _, out _, out _));
        Assert.IsFalse(CollectionRunEndpoints.TryCreateQuery(
            10, "not-a-cursor", out _, out _, out _));
    }

    [TestMethod]
    public void Delete_Status_Maps_NotFound_And_NonFailed_Run_To_Safe_Responses()
    {
        Assert.AreEqual(
            StatusCodes.Status200OK,
            CollectionRunEndpoints.GetDeleteStatusCode(CollectionRunDeleteOutcome.Deleted()));
        Assert.AreEqual(
            StatusCodes.Status404NotFound,
            CollectionRunEndpoints.GetDeleteStatusCode(
                CollectionRunDeleteOutcome.Failure("collection-run.not-found", "not found")));
        Assert.AreEqual(
            StatusCodes.Status409Conflict,
            CollectionRunEndpoints.GetDeleteStatusCode(
                CollectionRunDeleteOutcome.Failure("collection-run.not-failed", "not failed")));
    }
}
