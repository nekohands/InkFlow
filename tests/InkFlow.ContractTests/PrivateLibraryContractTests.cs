using System.Text.Json;
using InkFlow.Modules.Library.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ContractTests;

[TestClass]
public sealed class PrivateLibraryContractTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [TestMethod]
    public void Private_Chapter_Content_Uses_Private_Identity_And_Paragraphs()
    {
        var response = new PrivateChapterContentView(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            "第三章",
            "ABC123",
            ["第一段", "第二段"]);

        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(response, JsonOptions));
        var root = document.RootElement;

        Assert.IsTrue(root.TryGetProperty("privateChapterId", out _));
        Assert.IsTrue(root.TryGetProperty("privateBookId", out _));
        Assert.AreEqual(3, root.GetProperty("index").GetInt32());
        Assert.AreEqual("ABC123", root.GetProperty("contentHash").GetString());
        CollectionAssert.AreEqual(
            new[] { "第一段", "第二段" },
            root.GetProperty("paragraphs").EnumerateArray()
                .Select(item => item.GetString())
                .ToArray());

        Assert.IsFalse(root.TryGetProperty("bookId", out _));
        Assert.IsFalse(root.TryGetProperty("chapterId", out _));
        Assert.IsFalse(root.TryGetProperty("contentText", out _));
    }

    [TestMethod]
    public void Import_Response_Uses_Private_Book_View_And_Chapter_Count()
    {
        var response = new PrivateBookImportView(
            new PrivateBookView(
                Guid.NewGuid(),
                "私有书",
                "作者",
                DateTimeOffset.Parse("2026-08-28T00:00:00Z"),
                DateTimeOffset.Parse("2026-08-28T00:00:00Z")),
            2);

        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(response, JsonOptions));
        var root = document.RootElement;

        Assert.AreEqual(2, root.GetProperty("chapterCount").GetInt32());
        var book = root.GetProperty("book");
        Assert.IsTrue(book.TryGetProperty("privateBookId", out _));
        Assert.AreEqual("私有书", book.GetProperty("title").GetString());
        Assert.IsFalse(book.TryGetProperty("bookId", out _));
    }
}
