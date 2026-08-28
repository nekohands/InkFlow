using System.Security.Cryptography;
using System.Text;
using InkFlow.Modules.Library.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class PrivateContentTests
{
    private static readonly Guid UserId = Guid.Parse("01908d2a-2d44-7b3b-9ec2-123456789abc");
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Document_Normalizes_Paragraphs_And_Computes_Stable_Hash()
    {
        var document = PrivateContentDocument.FromParagraphs(
            ["  第一\n段  ", "第二段"]);

        Assert.AreEqual("第一 段\n\n第二段", document.CanonicalText);
        Assert.AreEqual(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document.CanonicalText))),
            document.ContentHash);
        Assert.AreEqual(2, document.Paragraphs.Count);
    }

    [TestMethod]
    public void Empty_And_Control_Content_Are_Rejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => PrivateContentDocument.FromParagraphs([" ", "\n"]));
        Assert.ThrowsExactly<ArgumentException>(
            () => PrivateContentDocument.FromParagraphs(["正文\0"]));
    }

    [TestMethod]
    public void Private_Chapter_Uses_Independent_Identity_And_Persisted_Checksum()
    {
        var bookId = Guid.CreateVersion7();
        var chapter = PrivateChapter.Create(
            UserId,
            bookId,
            0,
            " 第一章 ",
            PrivateContentDocument.FromParagraphs(["正文"]),
            T0);

        var loaded = PrivateChapter.Rehydrate(
            chapter.UserId,
            chapter.PrivateBookId,
            chapter.Id,
            chapter.Index,
            chapter.Title,
            chapter.ContentText,
            chapter.ContentHash,
            chapter.ParagraphCount,
            chapter.CreatedAt);

        Assert.AreNotEqual(Guid.Empty, chapter.Id);
        Assert.AreEqual("第一章", loaded.Title);
        Assert.AreEqual(chapter.ContentHash, loaded.ContentHash);
        Assert.ThrowsExactly<InvalidOperationException>(() => PrivateChapter.Rehydrate(
            chapter.UserId,
            chapter.PrivateBookId,
            chapter.Id,
            chapter.Index,
            chapter.Title,
            chapter.ContentText,
            new string('0', 64),
            chapter.ParagraphCount,
            chapter.CreatedAt));
    }
}
