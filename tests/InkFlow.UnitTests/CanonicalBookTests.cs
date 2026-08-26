using InkFlow.Modules.Library.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CanonicalBookTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 14, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Create_Trims_Metadata_And_Stamps_Timestamps()
    {
        var book = CanonicalBook.Create("  剑来  ", " 烽火戏诸侯 ", T0);

        Assert.AreEqual("剑来", book.Title);
        Assert.AreEqual("烽火戏诸侯", book.Author);
        Assert.AreEqual(T0, book.CreatedAt);
        Assert.AreEqual(0, book.Chapters.Count);
    }

    [TestMethod]
    public void Empty_Title_Or_Author_Are_Rejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CanonicalBook.Create("", "author", T0));
        Assert.ThrowsExactly<ArgumentException>(() => CanonicalBook.Create("title", " ", T0));
    }

    [TestMethod]
    public void Chapters_Must_Be_Appended_In_Order()
    {
        var book = CanonicalBook.Create("书", "作者", T0);

        var c0 = book.AddChapter(0, "第一章", T0);
        var c1 = book.AddChapter(1, "第二章", T0.AddMinutes(1));

        Assert.AreEqual(0, c0.Index);
        Assert.AreEqual(1, c1.Index);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => book.AddChapter(5, "跳章", T0));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => book.AddChapter(0, "重复", T0));
    }

    [TestMethod]
    public void UpdateMetadata_Changes_Title_And_Touches_UpdatedAt()
    {
        var book = CanonicalBook.Create("旧名", "作者", T0);
        book.UpdateMetadata("新名", "作者", T0.AddDays(1));

        Assert.AreEqual("新名", book.Title);
        Assert.AreEqual(T0, book.CreatedAt);
        Assert.AreEqual(T0.AddDays(1), book.UpdatedAt);
    }

    [TestMethod]
    public void Rehydrate_Preserves_Identity_And_Orders_Chapters()
    {
        var id = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var chapters = new[]
        {
            new CanonicalChapter(chapterId, id, 0, "第一章", T0),
            new CanonicalChapter(Guid.NewGuid(), id, 1, "第二章", T0),
        };

        var book = CanonicalBook.Rehydrate(id, "书", "作者", T0, T0, chapters);

        Assert.AreEqual(id, book.Id);
        Assert.AreEqual(2, book.Chapters.Count);
        Assert.AreEqual("第一章", book.Chapters[0].Title);
    }
}
