using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ReaderHtmlTests
{
    private static readonly BookListItem ListItem = new(Guid.NewGuid(), "剑来", "烽火戏诸侯", 2);

    private static readonly BookDetail Detail = new(
        Guid.NewGuid(), "剑来", "烽火戏诸侯",
        [
            new ChapterListItem(Guid.NewGuid(), 0, "第一章"),
            new ChapterListItem(Guid.NewGuid(), 1, "第二章"),
        ]);

    [TestMethod]
    public void BookList_Renders_Search_Form_And_Entries()
    {
        var html = ReaderHtml.BookListPage([ListItem], query: null);

        StringAssert.Contains(html, "viewport");
        StringAssert.Contains(html, "type=\"search\"");
        StringAssert.Contains(html, "/reader/books/");
        StringAssert.Contains(html, "剑来");
        StringAssert.Contains(html, "烽火戏诸侯");
    }

    [TestMethod]
    public void BookList_Empty_State_Is_Explained()
    {
        var html = ReaderHtml.BookListPage([], query: "不存在");
        StringAssert.Contains(html, "没有找到");
    }

    [TestMethod]
    public void Book_Detail_Has_Start_Reading_Primary_Action_And_Toc()
    {
        var html = ReaderHtml.BookDetailPage(Detail);

        StringAssert.Contains(html, "开始阅读");
        Assert.IsTrue(html.Contains("/reader/read/"), "详情页应有指向第一章的阅读链接");
        StringAssert.Contains(html, "目录");
        StringAssert.Contains(html, "第一章");
    }

    [TestMethod]
    public void Chapter_Page_Escapes_Html_In_Content_And_Titles()
    {
        var content = new ChapterContent(
            Guid.NewGuid(), Guid.NewGuid(), 0, "<script>alert(1)</script>",
            "example-source", ["正文<script>bad()</script>段落"]);
        var chapter = (Guid.NewGuid(), "<b>坏标题</b>");

        var html = ReaderHtml.ChapterPage(content, null, next: chapter, Guid.NewGuid(), "书");

        Assert.IsFalse(html.Contains("<script>alert(1)</script>"), "正文中的脚本必须被转义");
        StringAssert.Contains(html, "&lt;script&gt;");
    }

    [TestMethod]
    public void Chapter_Page_Shows_Previous_And_Next_Links()
    {
        var prevId = Guid.NewGuid();
        var nextId = Guid.NewGuid();
        var content = new ChapterContent(Guid.NewGuid(), Guid.NewGuid(), 1, "第二章", "src", ["正文"]);

        var html = ReaderHtml.ChapterPage(
            content,
            previous: (prevId, "第一章"),
            next: (nextId, "第三章"),
            Guid.NewGuid(), "书");

        StringAssert.Contains(html, $"reader/read/{prevId}");
        StringAssert.Contains(html, $"reader/read/{nextId}");
        StringAssert.Contains(html, "上一章");
        StringAssert.Contains(html, "下一章");
    }

    [TestMethod]
    public void First_Chapter_Has_No_Previous_Link()
    {
        var content = new ChapterContent(Guid.NewGuid(), Guid.NewGuid(), 0, "第一章", "src", ["正文"]);
        var html = ReaderHtml.ChapterPage(content, previous: null, next: (Guid.NewGuid(), "第二章"), Guid.NewGuid(), "书");

        Assert.IsFalse(html.Contains("上一章"));
        StringAssert.Contains(html, "下一章");
    }
}
