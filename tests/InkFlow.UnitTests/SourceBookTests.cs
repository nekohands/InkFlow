using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceBookTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Create_Trims_And_Validates()
    {
        var book = SourceBook.Create("src", "ext-1", "  书名  ", " 作者 ", T0);

        Assert.AreEqual("书名", book.Title);
        Assert.AreEqual("作者", book.Author);
        Assert.ThrowsExactly<ArgumentException>(
            () => SourceBook.Create("", "ext-1", "t", "a", T0));
        Assert.ThrowsExactly<ArgumentException>(
            () => SourceBook.Create("src", "", "t", "a", T0));
    }

    [TestMethod]
    public void SyncChapters_Is_Idempotent_By_External_Id()
    {
        var book = SourceBook.Create("src", "ext-1", "书名", "作者", T0);

        var first = book.SyncChapters(
        [
            ("c1", "第一章"),
            ("c2", "第二章"),
        ], T0);
        Assert.AreEqual(2, first.Count);

        // 同一批再次同步：全部已存在，无新增。
        var second = book.SyncChapters(
        [
            ("c1", "第一章"),
            ("c2", "第二章"),
        ], T0.AddMinutes(1));
        Assert.AreEqual(0, second.Count);
        Assert.AreEqual(2, book.Chapters.Count);

        // 索引连续追加，不因重复同步而错位。
        Assert.AreEqual(0, book.Chapters[0].Index);
        Assert.AreEqual(1, book.Chapters[1].Index);
    }

    [TestMethod]
    public void SyncChapters_Skips_Malformed_Entries_Within_Batch()
    {
        var book = SourceBook.Create("src", "ext-1", "书名", "作者", T0);

        book.SyncChapters(
        [
            ("", "空 ID"),
            ("c1", ""),
            ("c1", "重复 ID"),
            ("c2", "有效章节"),
        ], T0);

        Assert.AreEqual(1, book.Chapters.Count);
        Assert.AreEqual("c2", book.Chapters[0].ExternalChapterId);
    }

    [TestMethod]
    public async Task SyncChapters_Batches_Produce_Sequential_Indexes()
    {
        var book = SourceBook.Create("src", "ext-1", "书名", "作者", T0);

        var batch1 = book.SyncChapters([("c1", "一")], T0);
        var batch2 = await Task.Run(() => book.SyncChapters([("c2", "二"), ("c3", "三")], T0));

        Assert.AreEqual(1, batch1.Count);
        Assert.AreEqual(2, batch2.Count);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, book.Chapters.Select(c => c.Index).ToArray());
    }
}
