using System.Text.Json;
using InkFlow.Api;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;

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
}
