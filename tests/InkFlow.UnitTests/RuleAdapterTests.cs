using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class RuleAdapterTests
{
    private sealed class FakeHttpClient : ISourceHttpClient
    {
        public SourceHttpRequest? LastRequest { get; private set; }
        public int CallCount { get; private set; }
        public Func<SourceHttpRequest, SourceHttpResponse> Responder { get; set; } =
            _ => new SourceHttpResponse(200, """<a class="book-title" href="/book/12345">x</a>""");

        public Task<SourceHttpResponse> SendAsync(SourceHttpRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(Responder(request));
        }
    }

    private sealed class FakeSelectorEvaluator : ISelectorEvaluator
    {
        public Func<string, RuleSelector, string?> Handler { get; set; } = (_, selector) =>
            selector.Expression.Contains("title") || selector.Expression.EndsWith(".t")
                ? "  《示例书名》  "
                : null;

        public string? EvaluateFirst(string documentBody, RuleSelector selector) => Handler(documentBody, selector);
    }

    private const string BaseUrl = "https://books.example.com";

    private static CapabilityRule SearchRule() => new(
        SourceCapability.Search,
        RuleRequest.Get("/search?q={query}&page={page}"),
        [
            new RuleField("title", new RuleSelector(SelectorKind.Css, ".book-title"), null,
                [new TrimTransform(), new ReplaceTransform("《", ""), new ReplaceTransform("》", "")]),
            new RuleField("bookId",
                null,
                new RuleRegex(@"href=""/book/(\d+)""", 500),
                []),
        ]);

    [TestMethod]
    public async Task End_To_End_Search_Builds_Url_Extracts_And_Transforms()
    {
        var http = new FakeHttpClient();
        var adapter = new RuleAdapter(http, new FakeSelectorEvaluator());

        var result = await adapter.ExecuteAsync(
            SearchRule(),
            BaseUrl,
            new Dictionary<string, string> { ["query"] = "剑 来", ["page"] = "2" });

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual("示例书名", result.Values["title"]);
        Assert.AreEqual("12345", result.Values["bookId"]);

        // URL 组装：路径与 query 占位符填充 + 正确编码
        var url = http.LastRequest!.Url;
        StringAssert.Contains(url, "/search?");
        StringAssert.Contains(url, "q=%E5%89%91%20%E6%9D%A5");
        StringAssert.Contains(url, "page=2");
    }

    [TestMethod]
    public async Task Missing_Template_Variable_Fails_Without_Hitting_Http()
    {
        var http = new FakeHttpClient();
        var adapter = new RuleAdapter(http, new FakeSelectorEvaluator());

        var result = await adapter.ExecuteAsync(
            SearchRule(),
            BaseUrl,
            new Dictionary<string, string> { ["query"] = "x" }); // page 缺失

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("missing variable 'page'")));
        Assert.AreEqual(0, http.CallCount, "构建失败的请求绝不能出网");
    }

    [TestMethod]
    public async Task Internal_Base_Url_Is_Blocked_By_Ssrf_Guard()
    {
        var http = new FakeHttpClient();
        var adapter = new RuleAdapter(http, new FakeSelectorEvaluator());

        var result = await adapter.ExecuteAsync(
            SearchRule(),
            "http://127.0.0.1:8080",
            new Dictionary<string, string> { ["query"] = "q", ["page"] = "1" });

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.StartsWith("ssrf:")));
        Assert.AreEqual(0, http.CallCount, "SSRF 拒绝的请求绝不能出网");
    }

    [TestMethod]
    public async Task Upstream_Non_Success_Status_Fails_The_Run()
    {
        var http = new FakeHttpClient { Responder = _ => new SourceHttpResponse(503, "") };
        var adapter = new RuleAdapter(http, new FakeSelectorEvaluator());

        var result = await adapter.ExecuteAsync(
            SearchRule(), BaseUrl, new Dictionary<string, string> { ["query"] = "q", ["page"] = "1" });

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("503")));
    }

    [TestMethod]
    public async Task Regex_Extraction_Prefers_First_Capture_Group()
    {
        var rule = new CapabilityRule(
            SourceCapability.Content,
            RuleRequest.Get("/chapter/1"),
            [new RuleField("body", null, new RuleRegex(@"<p>(.*?)</p>", 200), [])]);

        var http = new FakeHttpClient { Responder = _ => new SourceHttpResponse(200, "<p>正文第一段</p>") };
        var result = await new RuleAdapter(http, new FakeSelectorEvaluator()).ExecuteAsync(rule, BaseUrl);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("正文第一段", result.Values["body"]);
    }

    [TestMethod]
    public async Task Catastrophic_Regex_Is_Reported_As_Timeout_Not_Hang()
    {
        var rule = new CapabilityRule(
            SourceCapability.Content,
            RuleRequest.Get("/chapter/1"),
            [new RuleField("body", null, new RuleRegex(@"^(a+)+$", 100), [])]);

        var longInput = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!";
        var http = new FakeHttpClient { Responder = _ => new SourceHttpResponse(200, longInput) };
        var result = await new RuleAdapter(http, new FakeSelectorEvaluator()).ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("timed out")));
    }

    [TestMethod]
    public async Task Missing_Field_Match_Lists_The_Field_In_Errors()
    {
        var evaluator = new FakeSelectorEvaluator { Handler = (_, _) => null };
        var http = new FakeHttpClient { Responder = _ => new SourceHttpResponse(200, "<html/>") };
        var result = await new RuleAdapter(http, evaluator).ExecuteAsync(
            SearchRule(), BaseUrl, new Dictionary<string, string> { ["query"] = "q", ["page"] = "1" });

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("'title'")));
        Assert.IsTrue(result.Errors.Any(e => e.Contains("'bookId'")));
    }

    [TestMethod]
    public async Task Post_Rule_Sends_Form_Body()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Post,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["keyword"] = "{query}" }),
            [new RuleField("title", new RuleSelector(SelectorKind.Css, ".t"), null, [])]);

        var http = new FakeHttpClient { Responder = _ => new SourceHttpResponse(200, "<t>结果</t>") };
        var result = await new RuleAdapter(http, new FakeSelectorEvaluator()).ExecuteAsync(
            rule, BaseUrl, new Dictionary<string, string> { ["query"] = "k" });

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual(RuleHttpMethod.Post, http.LastRequest!.Method);
        Assert.AreEqual("keyword=k", http.LastRequest.FormBody);
    }

    [TestMethod]
    public async Task Transport_Exception_Is_Classified_As_Transport_Failure()
    {
        var failingHttp = new ThrowingHttpClient();
        var result = await new RuleAdapter(failingHttp, new FakeSelectorEvaluator()).ExecuteAsync(
            SearchRule(), BaseUrl, new Dictionary<string, string> { ["query"] = "q", ["page"] = "1" });

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("transport failure")));
    }

    private sealed class ThrowingHttpClient : ISourceHttpClient
    {
        public Task<SourceHttpResponse> SendAsync(SourceHttpRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("connection refused (fixture)");
    }
}
