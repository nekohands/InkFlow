using System.Text.Json;
using InkFlow.BuildingBlocks.Messaging;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class MessagingContractTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 29, 14, 0, 0, TimeSpan.FromHours(8));

    [TestMethod]
    public void Integration_Message_Normalizes_And_Hashes_Valid_Json()
    {
        var id = Guid.CreateVersion7();
        var message = IntegrationMessage.Create(
            "  catalog.book.created ",
            "{\"bookId\":\"42\"}",
            OccurredAt,
            traceId: " trace-1 ",
            id: id);

        Assert.AreEqual(id, message.Id);
        Assert.AreEqual("catalog.book.created", message.MessageType);
        Assert.AreEqual(OccurredAt.ToUniversalTime(), message.OccurredAt);
        Assert.AreEqual("trace-1", message.TraceId);
        Assert.AreEqual(64, message.PayloadHash.Length);
        Assert.AreEqual(
            message.PayloadHash,
            Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(message.Payload)))
                .ToLowerInvariant());
    }

    [TestMethod]
    public void Integration_Message_Rejects_Invalid_Or_Overlarge_Payload()
    {
        Assert.Throws<ArgumentException>(() =>
            IntegrationMessage.Create("test", "not-json", OccurredAt));

        var oversized = "\"" + new string('x', IntegrationMessage.MaxPayloadBytes) + "\"";
        Assert.Throws<ArgumentException>(() =>
            IntegrationMessage.Create("test", oversized, OccurredAt));

        Assert.Throws<ArgumentException>(() =>
            IntegrationMessage.Create("test", "{}", OccurredAt, id: Guid.Empty));
    }

    [TestMethod]
    public void Crawler_Task_Created_Message_Excludes_Variables_And_Credentials()
    {
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "official-a",
                SourceCapability.Content,
                new Dictionary<string, string>
                {
                    ["bookId"] = "book-42",
                    ["chapterId"] = "chapter-7",
                },
                "secret-reference"),
            createdAt: OccurredAt);

        var message = CrawlerIntegrationMessages.TaskCreated(task);
        using var payload = JsonDocument.Parse(message.Payload);

        Assert.AreEqual(CrawlerIntegrationMessages.TaskCreatedType, message.MessageType);
        Assert.AreEqual(task.Id, message.Id);
        Assert.IsTrue(payload.RootElement.TryGetProperty("taskId", out _));
        Assert.IsTrue(payload.RootElement.TryGetProperty("sourceId", out _));
        Assert.IsFalse(message.Payload.Contains("book-42", StringComparison.Ordinal));
        Assert.IsFalse(message.Payload.Contains("chapter-7", StringComparison.Ordinal));
        Assert.IsFalse(message.Payload.Contains("secret-reference", StringComparison.Ordinal));
    }
}
