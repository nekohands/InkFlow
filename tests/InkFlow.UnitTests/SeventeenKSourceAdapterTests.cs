using System.Net;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Sources.Adapters.SeventeenK;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SeventeenKSourceAdapterTests
{
    [TestMethod]
    public async Task Search_Parses_Stable_Book_Identity_And_Deduplicates()
    {
        var handler = new FixtureHandler((request, _) => request.RequestUri!.AbsolutePath.Contains("/search")
            ? Json("""
                {"data":[
                  {"book_id":123,"book_name":"星河入梦","author_name":"甲"},
                  {"book_id":"00123","book_name":"重复结果","author_name":"乙"},
                  {"book_id":"bad/id","book_name":"不安全结果","author_name":"丙"}
                ]}
                """)
            : Json("{}"));
        var adapter = CreateAdapter(handler);

        var results = await adapter.SearchAsync(" 星河 ");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("123", results[0].ExternalBookId);
        Assert.AreEqual("星河入梦", results[0].Title);
        Assert.AreEqual("甲", results[0].Author);
        StringAssert.Contains(handler.Requests.Single().RequestUri!.Query, "key=%E6%98%9F%E6%B2%B3");
    }

    [TestMethod]
    public async Task BookInfo_Toc_And_Free_Content_Use_Expected_External_Ids()
    {
        var handler = new FixtureHandler((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/book/123/split1/merge" => Json("""
                {"data":[{"bookTop":{"bookName":"星河入梦","authorPenName":"甲"}}]}
                """),
            "/v2/book/123/volumes" => Json("""
                {"data":{"book_id":123,"volumes":[
                  {"volume_name":"第一卷","chapters":[
                    {"chapter_id":456,"name":"第一章"},
                    {"chapter_id":"457","name":"第二章"}
                  ]}
                ]}}
                """),
            "/ck/book/123/chapter/456" => Json("""
                {"data":{"isVIP":{"id":0},"content":[
                  {"text":"第一段"},{"text":"第二段"}
                ]}}
                """),
            _ => Json("{}"),
        });
        var adapter = CreateAdapter(handler);

        var info = await adapter.GetBookInfoAsync("123");
        var toc = await adapter.GetTableOfContentsAsync("123");
        var content = await adapter.GetChapterContentAsync(toc[0].ExternalChapterId);

        Assert.IsNotNull(info);
        Assert.AreEqual("星河入梦", info.Title);
        Assert.AreEqual("甲", info.Author);
        Assert.AreEqual(2, toc.Count);
        Assert.AreEqual("123/456", toc[0].ExternalChapterId);
        Assert.AreEqual(0, toc[0].Index);
        Assert.AreEqual("123/457", toc[1].ExternalChapterId);
        Assert.AreEqual("第一段\n\n第二段", content);
        Assert.AreEqual(3, handler.Requests.Count);
    }

    [TestMethod]
    public async Task Unpurchased_Vip_Content_Is_Not_Bypassed()
    {
        var handler = new FixtureHandler((_, _) => Json("""
            {"data":{"isVIP":{"id":1},"userReadInfo":{"free":0},"content":[{"text":"不应返回"}]}}
            """));
        var adapter = CreateAdapter(handler);

        var content = await adapter.GetChapterContentAsync("123/456");

        Assert.IsNull(content);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task Invalid_Identifiers_Do_Not_Touch_The_Network()
    {
        var handler = new FixtureHandler((_, _) => Json("{}"));
        var adapter = CreateAdapter(handler);

        Assert.IsNull(await adapter.GetBookInfoAsync("123/../../private"));
        Assert.AreEqual(0, (await adapter.GetTableOfContentsAsync("not-numeric")).Count);
        Assert.IsNull(await adapter.GetChapterContentAsync("123/456/789"));
        Assert.AreEqual(0, handler.Requests.Count);
    }

    private static SeventeenKSourceAdapter CreateAdapter(FixtureHandler handler) =>
        new(new HttpClient(handler), new FixedResolver());

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        };

    private sealed class FixtureHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request, cancellationToken));
        }
    }

    private sealed class FixedResolver : IIpAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IPAddress>>(
                [IPAddress.Parse("104.21.0.7")]);
    }
}
