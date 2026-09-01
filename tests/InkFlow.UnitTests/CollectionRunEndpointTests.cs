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
}
