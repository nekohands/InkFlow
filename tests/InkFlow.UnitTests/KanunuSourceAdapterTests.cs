using System.Net;
using System.Text;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Sources.Adapters.Kanunu8;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class KanunuSourceAdapterTests
{
    private const string BookPage = """
        <html><head><title>玉簟秋 by 灵希 - 小说在线阅读 - 努努书坊</title></head>
        <body>
        <h1>玉簟秋</h1>
        <ul>
            <li><a href="442395.html">第一回 金陵繁花红粉伴衣香</a></li>
            <li><a href="442396.html">第二回 公子腮上胭脂惊父颜</a></li>
        </ul>
        </body></html>
        """;

    private const string ChapterPage = """
        <html><head><title>正文 第一回</title></head>
        <body>
        <h1>玉簟秋 正文 第一回</h1>
        <p>时值金陵六月，初夏时节，阳光分外的暖。</p>
        <p>花园里的几棵树木间摆放着许多盆景。</p>
        <p>&nbsp;</p>
        </body></html>
        """;

    [TestMethod]
    public async Task BookInfo_Parses_Title_And_Author_From_Gb18030_Page()
    {
        var adapter = CreateAdapter(BookPage);
        var info = await adapter.GetBookInfoAsync("book/3441");

        Assert.IsNotNull(info);
        Assert.AreEqual("玉簟秋", info.Title);
        Assert.AreEqual("灵希", info.Author);
    }

    [TestMethod]
    public async Task Toc_Extracts_Chapters_With_Self_Locating_Ids()
    {
        var adapter = CreateAdapter(BookPage);
        var toc = await adapter.GetTableOfContentsAsync("book/3441");

        Assert.AreEqual(2, toc.Count);
        Assert.AreEqual("book/3441/442395.html", toc[0].ExternalChapterId, "章节 ID 应自包含定位路径");
        Assert.AreEqual(0, toc[0].Index);
        Assert.AreEqual(1, toc[1].Index);
    }

    [TestMethod]
    public async Task Content_Extracts_NonEmpty_Paragraphs()
    {
        var adapter = CreateAdapter(ChapterPage);
        var content = await adapter.GetChapterContentAsync("book/3441/442395.html");

        Assert.IsNotNull(content);
        StringAssert.Contains(content, "时值金陵六月");
        // 空实体段落应被过滤。
        Assert.IsFalse(content.Contains("&nbsp;"));
    }

    private static KanunuSourceAdapter CreateAdapter(string html)
    {
        var handler = new FakeHandler(SourceEncodings.Gb18030.GetBytes(html));
        return new KanunuSourceAdapter(new HttpClient(handler), new FixedResolver());
    }

    private sealed class FakeHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
    }

    private sealed class FixedResolver : IIpAddressResolver
    {
        public Task<IReadOnlyList<System.Net.IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<System.Net.IPAddress>>(
                [System.Net.IPAddress.Parse("104.21.0.7")]); // 公网地址,SSRF 校验放行
    }
}
