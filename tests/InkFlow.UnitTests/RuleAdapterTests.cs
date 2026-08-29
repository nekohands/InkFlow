using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
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

        public string? EvaluateFirst(string documentBody, RuleSelector selector, string? attributeName = null) => Handler(documentBody, selector);

        public IReadOnlyList<SelectorElementSnapshot> SelectAll(string documentBody, RuleSelector selector) => [];
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
    public async Task Request_Form_Over_Max_Bytes_Fails_Without_Hitting_Http()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Post,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["keyword"] = "123456" }),
            []);
        var http = new FakeHttpClient();
        var limits = new SourceRuleExecutionLimits { MaxBytes = 5 };

        var result = await new RuleAdapter(http, new FakeSelectorEvaluator(), limits)
            .ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("request exceeded byte budget")));
        Assert.AreEqual(0, http.CallCount);
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

    [TestMethod]
    public async Task Request_Budget_Zero_Fails_Without_Hitting_Http()
    {
        var http = new FakeHttpClient();
        var limits = new SourceRuleExecutionLimits { MaxRequests = 0 };
        var adapter = new RuleAdapter(http, new FakeSelectorEvaluator(), limits);

        var result = await adapter.ExecuteAsync(
            SearchRule(), BaseUrl, new Dictionary<string, string> { ["query"] = "q", ["page"] = "1" });

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("request budget exceeded")));
        Assert.AreEqual(0, http.CallCount, "a request denied by the budget must not reach the HTTP seam");
    }

    [TestMethod]
    public async Task Execution_Time_Budget_Cancels_A_Hanging_Request()
    {
        var http = new HangingHttpClient();
        var limits = new SourceRuleExecutionLimits
        {
            MaxExecutionTime = TimeSpan.FromMilliseconds(40),
        };
        var adapter = new RuleAdapter(http, new FakeSelectorEvaluator(), limits);
        using var callerCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var execution = adapter.ExecuteAsync(
            SearchRule(),
            BaseUrl,
            new Dictionary<string, string> { ["query"] = "q", ["page"] = "1" },
            callerCancellation.Token);
        var completed = await Task.WhenAny(execution, Task.Delay(TimeSpan.FromMilliseconds(500)));

        Assert.AreSame(execution, completed, "the execution budget must bound a non-returning HTTP call");
        var result = await execution;

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("time budget exceeded")));
        await Task.Delay(TimeSpan.FromMilliseconds(20));
        Assert.IsTrue(http.CancellationRequested);
    }

    [TestMethod]
    public async Task Response_Over_Max_Bytes_Fails_Before_Field_Extraction()
    {
        var http = new FakeHttpClient
        {
            Responder = _ => new SourceHttpResponse(200, "123456"),
        };
        var limits = new SourceRuleExecutionLimits { MaxBytes = 5 };
        var adapter = new RuleAdapter(http, new FakeSelectorEvaluator(), limits);

        var result = await adapter.ExecuteAsync(
            SearchRule(),
            BaseUrl,
            new Dictionary<string, string> { ["query"] = "q", ["page"] = "1" });

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("response exceeded byte budget")));
    }

    [TestMethod]
    public async Task Extracted_Result_Over_Max_Size_Fails_Without_Returning_Partial_Data()
    {
        var rule = new CapabilityRule(
            SourceCapability.Content,
            RuleRequest.Get("/chapter/1"),
            [new RuleField("content", new RuleSelector(SelectorKind.Css, ".content"), null, [])]);
        var http = new FakeHttpClient
        {
            Responder = _ => new SourceHttpResponse(200, "<html/>")
        };
        var evaluator = new FakeSelectorEvaluator { Handler = (_, _) => "123456" };
        var limits = new SourceRuleExecutionLimits { MaxResultSize = 5 };

        var result = await new RuleAdapter(http, evaluator, limits).ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("result exceeded size budget")));
        Assert.AreEqual(0, result.Values.Count, "a result over budget must not expose partial fields");
    }

    [TestMethod]
    public async Task Regex_Uses_The_Stricter_Execution_Time_Budget()
    {
        var rule = new CapabilityRule(
            SourceCapability.Content,
            RuleRequest.Get("/chapter/1"),
            [new RuleField("body", null, new RuleRegex(@"^(a+)+$", 1_000), [])]);
        var http = new FakeHttpClient
        {
            Responder = _ => new SourceHttpResponse(200, new string('a', 100) + "!"),
        };
        var limits = new SourceRuleExecutionLimits
        {
            MaxRegexTime = TimeSpan.FromMilliseconds(1),
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await new RuleAdapter(http, new FakeSelectorEvaluator(), limits)
            .ExecuteAsync(rule, BaseUrl);
        stopwatch.Stop();

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("regex evaluation timed out")));
        Assert.IsTrue(
            stopwatch.Elapsed < TimeSpan.FromMilliseconds(250),
            $"the stricter regex budget should finish promptly, actual {stopwatch.Elapsed}");
    }

    [TestMethod]
    public async Task Oversized_Response_Is_Classified_As_A_Budget_Failure()
    {
        var result = await new OversizedResponseHttpClient()
            .CreateAdapter()
            .ExecuteAsync(
                SearchRule(),
                BaseUrl,
                new Dictionary<string, string> { ["query"] = "q", ["page"] = "1" });

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("response exceeded byte budget")));
        Assert.IsFalse(result.Errors.Any(e => e.Contains("transport failure")));
    }

    [TestMethod]
    public async Task Next_Link_Pagination_Returns_All_Page_Bodies_Within_Budget()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 4));
        var http = new FakeHttpClient
        {
            Responder = request => request.Url switch
            {
                "https://books.example.com/search?page=1" => new SourceHttpResponse(
                    200,
                    "<a class=\"result\" href=\"/book/1\">one</a><a class=\"next\" href=\"/search?page=2\">next</a>"),
                "https://books.example.com/search?page=2" => new SourceHttpResponse(
                    200,
                    "<a class=\"result\" href=\"/book/2\">two</a><a class=\"next\" href=\"/search?page=3\">next</a>"),
                "https://books.example.com/search?page=3" => new SourceHttpResponse(
                    200,
                    "<a class=\"result\" href=\"/book/3\">three</a>"),
                _ => new SourceHttpResponse(404, string.Empty),
            },
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 3 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual(3, http.CallCount);
        Assert.AreEqual(3, result.ResponseBodies.Count);
        StringAssert.Contains(result.ResponseBodies[0], "href=\"/book/1\"");
        StringAssert.Contains(result.ResponseBodies[1], "href=\"/book/2\"");
        StringAssert.Contains(result.ResponseBodies[2], "href=\"/book/3\"");
    }

    [TestMethod]
    public async Task Next_Link_Pagination_Fails_Closed_When_Request_Budget_Is_Exhausted()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 4));
        var http = new FakeHttpClient
        {
            Responder = _ => new SourceHttpResponse(
                200,
                "<a class=\"result\" href=\"/book/1\">one</a><a class=\"next\" href=\"/search?page=2\">next</a>"),
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 1 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("request budget exceeded")));
        Assert.AreEqual(1, http.CallCount);
        Assert.AreEqual(0, result.ResponseBodies.Count, "a truncated pagination run must not expose pages");
    }

    [TestMethod]
    public async Task Next_Link_Pagination_Rejects_Cross_Origin_Links()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 4));
        var http = new FakeHttpClient
        {
            Responder = _ => new SourceHttpResponse(
                200,
                "<a class=\"result\" href=\"/book/1\">one</a><a class=\"next\" href=\"https://other.example.com/page-2\">next</a>"),
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 2 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("source origin")));
        Assert.AreEqual(1, http.CallCount);
        Assert.AreEqual(0, result.ResponseBodies.Count);
    }

    [TestMethod]
    public async Task Next_Link_Pagination_Fails_Closed_When_Page_Limit_Is_Exhausted()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 1));
        var http = new FakeHttpClient
        {
            Responder = _ => new SourceHttpResponse(
                200,
                "<a class=\"result\" href=\"/book/1\">one</a><a class=\"next\" href=\"/search?page=2\">next</a>"),
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 4 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("page limit exceeded")));
        Assert.AreEqual(1, http.CallCount);
        Assert.AreEqual(0, result.ResponseBodies.Count);
    }

    [TestMethod]
    public async Task Next_Link_Pagination_Rejects_Cycles_Before_Repeating_A_Request()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 4));
        var http = new FakeHttpClient
        {
            Responder = _ => new SourceHttpResponse(
                200,
                "<a class=\"result\" href=\"/book/1\">one</a><a class=\"next\" href=\"/search?page=1\">next</a>"),
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 4 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("cycle")));
        Assert.AreEqual(1, http.CallCount);
        Assert.AreEqual(0, result.ResponseBodies.Count);
    }

    [TestMethod]
    public async Task Followed_Next_Links_Use_Get_And_Drop_The_Initial_Form()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Post,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["query"] = "keyword" }),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 2));
        var methods = new List<RuleHttpMethod>();
        var forms = new List<string?>();
        var http = new FakeHttpClient
        {
            Responder = request =>
            {
                methods.Add(request.Method);
                forms.Add(request.FormBody);
                return methods.Count == 1
                    ? new SourceHttpResponse(
                        200,
                        "<a class=\"result\" href=\"/book/1\">one</a><a class=\"next\" href=\"/search?page=2\">next</a>")
                    : new SourceHttpResponse(200, "<a class=\"result\" href=\"/book/2\">two</a>");
            },
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 2 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        CollectionAssert.AreEqual(
            new[] { RuleHttpMethod.Post, RuleHttpMethod.Get },
            methods.ToArray());
        Assert.AreEqual("query=keyword", forms[0]);
        Assert.IsNull(forms[1]);
    }

    [TestMethod]
    public async Task Paginated_Response_Bodies_Share_One_Byte_Budget()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 2));
        var http = new FakeHttpClient
        {
            Responder = request => request.Url.EndsWith("page=1", StringComparison.Ordinal)
                ? new SourceHttpResponse(
                    200,
                    "<a class=\"next\" href=\"/search?page=2\">next</a>" + new string('x', 80))
                : new SourceHttpResponse(200, new string('y', 80)),
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 2, MaxBytes = 170 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("response exceeded byte budget")));
        Assert.AreEqual(2, http.CallCount);
        Assert.AreEqual(0, result.ResponseBodies.Count);
    }

    [TestMethod]
    public async Task Page_Number_Pagination_Advances_The_Declared_Query_Parameter()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Get,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    ["page"] = "ignored",
                    ["q"] = "keyword",
                },
                new Dictionary<string, string>()),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 4)
            {
                Mode = RulePaginationMode.PageNumber,
                ParameterName = "page",
                StartPage = 1,
                PageStep = 1,
            });
        var requests = new List<SourceHttpRequest>();
        var http = new FakeHttpClient
        {
            Responder = request =>
            {
                requests.Add(request);
                return request.Url.Contains("page=1", StringComparison.Ordinal)
                    ? new SourceHttpResponse(
                        200,
                        "<a class=\"result\" href=\"/book/1\">one</a><a class=\"next\" href=\"/ignored\">next</a>")
                    : new SourceHttpResponse(200, "<a class=\"result\" href=\"/book/2\">two</a>");
            },
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 3 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual(2, requests.Count);
        StringAssert.Contains(requests[0].Url, "page=1");
        StringAssert.Contains(requests[0].Url, "q=keyword");
        StringAssert.Contains(requests[1].Url, "page=2");
        StringAssert.Contains(requests[1].Url, "q=keyword");
        Assert.AreEqual(2, result.ResponseBodies.Count);
    }

    [TestMethod]
    public async Task Page_Number_Pagination_Updates_A_Post_Form_And_Preserves_The_Method()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Post,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    ["page"] = "ignored",
                    ["q"] = "keyword",
                }),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 2)
            {
                Mode = RulePaginationMode.PageNumber,
                ParameterName = "page",
                StartPage = 1,
                PageStep = 1,
            });
        var requests = new List<SourceHttpRequest>();
        var http = new FakeHttpClient
        {
            Responder = request =>
            {
                requests.Add(request);
                return requests.Count == 1
                    ? new SourceHttpResponse(200, "<a class=\"next\" href=\"/ignored\">next</a>")
                    : new SourceHttpResponse(200, "done");
            },
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 2 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        CollectionAssert.AreEqual(
            new[] { RuleHttpMethod.Post, RuleHttpMethod.Post },
            requests.Select(request => request.Method).ToArray());
        Assert.AreEqual("page=1&q=keyword", requests[0].FormBody);
        Assert.AreEqual("page=2&q=keyword", requests[1].FormBody);
    }

    [TestMethod]
    public async Task Cursor_Pagination_Injects_The_Selected_Cursor_And_Stops_When_Absent()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Get,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["cursor"] = string.Empty },
                new Dictionary<string, string>()),
            [],
            List: new RuleListBinding("$.items[*]", "id", string.Empty, string.Empty, SelectorKind.JsonPath),
            Pagination: new RulePagination(
                MaxPages: 4)
            {
                Mode = RulePaginationMode.Cursor,
                ParameterName = "cursor",
                CursorSelector = new RuleSelector(SelectorKind.JsonPath, "$.nextCursor"),
            });
        var requests = new List<SourceHttpRequest>();
        var http = new FakeHttpClient
        {
            Responder = request =>
            {
                requests.Add(request);
                return requests.Count == 1
                    ? new SourceHttpResponse(200, "{\"items\":[],\"nextCursor\":\"a b\"}")
                    : new SourceHttpResponse(200, "{\"items\":[]}");
            },
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 3 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual(2, requests.Count);
        StringAssert.Contains(requests[0].Url, "cursor=");
        StringAssert.Contains(requests[1].Url, "cursor=a%20b");
        Assert.AreEqual(2, result.ResponseBodies.Count);
    }

    [TestMethod]
    public async Task Cursor_Pagination_Fails_Closed_When_A_Cursor_Repeats()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Get,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["cursor"] = string.Empty },
                new Dictionary<string, string>()),
            [],
            List: new RuleListBinding("$.items[*]", "id", string.Empty, string.Empty, SelectorKind.JsonPath),
            Pagination: new RulePagination(MaxPages: 4)
            {
                Mode = RulePaginationMode.Cursor,
                ParameterName = "cursor",
                CursorSelector = new RuleSelector(SelectorKind.JsonPath, "$.nextCursor"),
            });
        var http = new FakeHttpClient
        {
            Responder = _ => new SourceHttpResponse(
                200,
                "{\"items\":[],\"nextCursor\":\"same\"}"),
        };
        var adapter = new RuleAdapter(
            http,
            new RuleSelectorEvaluator(),
            new SourceRuleExecutionLimits { MaxRequests = 4 });

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("cursor cycle")));
        Assert.AreEqual(2, http.CallCount);
        Assert.AreEqual(0, result.ResponseBodies.Count);
    }

    [TestMethod]
    public async Task Page_Number_Pagination_Requires_The_Declared_Parameter()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search"),
            [],
            List: new RuleListBinding("a.result", "href", string.Empty, string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 2)
            {
                Mode = RulePaginationMode.PageNumber,
                ParameterName = "page",
            });
        var http = new FakeHttpClient();
        var adapter = new RuleAdapter(http, new RuleSelectorEvaluator());

        var result = await adapter.ExecuteAsync(rule, BaseUrl);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("declared exactly once")));
        Assert.AreEqual(0, http.CallCount);
    }

    private sealed class ThrowingHttpClient : ISourceHttpClient
    {
        public Task<SourceHttpResponse> SendAsync(SourceHttpRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("connection refused (fixture)");
    }

    private sealed class OversizedResponseHttpClient : ISourceHttpClient
    {
        public RuleAdapter CreateAdapter() => new(this, new FakeSelectorEvaluator());

        public Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default) =>
            throw new SourceResponseTooLargeException();
    }

    private sealed class HangingHttpClient : ISourceHttpClient
    {
        public bool CancellationRequested { get; private set; }

        public async Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default)
        {
            using var registration = cancellationToken.Register(() => CancellationRequested = true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new InvalidOperationException("the hanging fixture unexpectedly completed");
        }
    }
}
