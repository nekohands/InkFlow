using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class RuleBasedSourceAdapterPaginationTests
{
    private sealed class PagingHttpClient : ISourceHttpClient
    {
        public int CallCount { get; private set; }

        public Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var response = request.Url switch
            {
                "https://books.example.com/search?page=1" =>
                    "<a class=\"book\" href=\"/book/1\">One</a><a class=\"next\" href=\"/search?page=2\">Next</a>",
                "https://books.example.com/search?page=2" =>
                    "<a class=\"book\" href=\"/book/2\">Two</a><a class=\"next\" href=\"/search?page=3\">Next</a>",
                "https://books.example.com/search?page=3" =>
                    "<a class=\"book\" href=\"/book/3\">Three</a>",
                "https://books.example.com/book/1" =>
                    "<a class=\"chapter\" href=\"/chapter/1\">Chapter</a>" +
                    "<a class=\"chapter\" href=\"/chapter/2\">Chapter 2</a>",
                _ => string.Empty,
            };

            return Task.FromResult(new SourceHttpResponse(
                response.Length == 0 ? 404 : 200,
                response));
        }
    }

    private sealed class JsonPagingHttpClient : ISourceHttpClient
    {
        public int CallCount { get; private set; }

        public Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var response = request.Url switch
            {
                "https://books.example.com/search?page=1" =>
                    "{\"items\":[{\"id\":\"book-1\",\"title\":\"One\"}],\"next\":\"/search?page=2\"}",
                "https://books.example.com/search?page=2" =>
                    "{\"items\":[{\"id\":\"book-2\",\"title\":\"Two\"}]}",
                _ => string.Empty,
            };

            return Task.FromResult(new SourceHttpResponse(
                response.Length == 0 ? 404 : 200,
                response));
        }
    }

    private sealed class PageNumberHttpClient : ISourceHttpClient
    {
        public int CallCount { get; private set; }

        public Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var response = request.Url switch
            {
                "https://books.example.com/search?page=1" =>
                    "<a class=\"book\" href=\"/book/1\">One</a><a class=\"next\" href=\"/ignored\">Next</a>",
                "https://books.example.com/search?page=2" =>
                    "<a class=\"book\" href=\"/book/2\">Two</a>",
                _ => string.Empty,
            };

            return Task.FromResult(new SourceHttpResponse(
                response.Length == 0 ? 404 : 200,
                response));
        }
    }

    private sealed class SlowListSelectorEvaluator : ISelectorEvaluator
    {
        public string? EvaluateFirst(
            string documentBody,
            RuleSelector selector,
            string? attributeName = null) => null;

        public IReadOnlyList<SelectorElementSnapshot> SelectAll(
            string documentBody,
            RuleSelector selector)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(60));
            return
            [
                new SelectorElementSnapshot(
                    "One",
                    new Dictionary<string, string> { ["href"] = "/book/1" }),
            ];
        }
    }

    [TestMethod]
    public async Task Search_Projects_Items_From_All_Paginated_Bodies()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 4));
        var source = Source.Rehydrate(
            "paged-source",
            "分页来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "paged-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var http = new PagingHttpClient();
        var limits = new SourceRuleExecutionLimits { MaxRequests = 3 };
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(http, new RuleSelectorEvaluator(), limits),
            new RuleSelectorEvaluator(),
            limits);

        var results = await adapter.SearchAsync("keyword");

        Assert.AreEqual(3, http.CallCount);
        Assert.AreEqual(3, results.Count);
        CollectionAssert.AreEqual(
            new[] { "1", "2", "3" },
            results.Select(result => result.ExternalBookId).ToArray());
    }

    [TestMethod]
    public async Task Search_Follows_Json_Next_Link_And_Projects_Json_Items()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding(
                "$.items[*]",
                "id",
                string.Empty,
                string.Empty,
                SelectorKind.JsonPath,
                "title"),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.JsonPath, "$.next"),
                null,
                MaxPages: 3));
        var source = Source.Rehydrate(
            "json-paged-source",
            "JSON 分页来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "json-paged-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var http = new JsonPagingHttpClient();
        var limits = new SourceRuleExecutionLimits { MaxRequests = 2 };
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(http, new RuleSelectorEvaluator(), limits),
            new RuleSelectorEvaluator(),
            limits);

        var results = await adapter.SearchAsync("keyword");

        Assert.AreEqual(2, http.CallCount);
        Assert.AreEqual(2, results.Count);
        CollectionAssert.AreEqual(
            new[] { "book-1", "book-2" },
            results.Select(result => result.ExternalBookId).ToArray());
        CollectionAssert.AreEqual(
            new[] { "One", "Two" },
            results.Select(result => result.Title).ToArray());
    }

    [TestMethod]
    public async Task Search_Projects_Items_From_Page_Number_Pagination()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Get,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["page"] = "1" },
                new Dictionary<string, string>()),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 3)
            {
                Mode = RulePaginationMode.PageNumber,
                ParameterName = "page",
                StartPage = 1,
                PageStep = 1,
            });
        var source = Source.Rehydrate(
            "page-number-source",
            "页码来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "page-number-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var http = new PageNumberHttpClient();
        var limits = new SourceRuleExecutionLimits { MaxRequests = 3 };
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(http, new RuleSelectorEvaluator(), limits),
            new RuleSelectorEvaluator(),
            limits);

        var results = await adapter.SearchAsync("keyword");

        Assert.AreEqual(2, http.CallCount);
        CollectionAssert.AreEqual(
            new[] { "1", "2" },
            results.Select(result => result.ExternalBookId).ToArray());
    }

    [TestMethod]
    public async Task Search_List_Extraction_Fails_Closed_After_Execution_Deadline()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty));
        var source = Source.Rehydrate(
            "slow-list-source",
            "慢列表来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "slow-list-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var limits = new SourceRuleExecutionLimits
        {
            MaxExecutionTime = TimeSpan.FromMilliseconds(10),
        };
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(
                new PagingHttpClient(),
                new RuleSelectorEvaluator(),
                limits),
            new SlowListSelectorEvaluator(),
            limits);

        var results = await adapter.SearchAsync("keyword");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task Toc_List_Extraction_Fails_Closed_After_Execution_Deadline()
    {
        var rule = new CapabilityRule(
            SourceCapability.Toc,
            RuleRequest.Get("/book/1"),
            [],
            List: new RuleListBinding("a.chapter", "href", "/chapter/", string.Empty));
        var source = Source.Rehydrate(
            "slow-toc-source",
            "慢目录来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "slow-toc-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var limits = new SourceRuleExecutionLimits
        {
            MaxExecutionTime = TimeSpan.FromMilliseconds(10),
        };
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(
                new PagingHttpClient(),
                new RuleSelectorEvaluator(),
                limits),
            new SlowListSelectorEvaluator(),
            limits);

        var entries = await adapter.GetTableOfContentsAsync("1");

        Assert.AreEqual(0, entries.Count);
    }

    [TestMethod]
    public async Task Search_List_Extraction_Fails_Closed_After_Result_Item_Budget()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search?page=1"),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                MaxPages: 4));
        var source = Source.Rehydrate(
            "bounded-search-source",
            "有界搜索来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "bounded-search-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var http = new PagingHttpClient();
        var limits = new SourceRuleExecutionLimits
        {
            MaxRequests = 3,
            MaxResultItems = 2,
        };
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(http, new RuleSelectorEvaluator(), limits),
            new RuleSelectorEvaluator(),
            limits);

        var results = await adapter.SearchAsync("keyword");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task Toc_List_Extraction_Fails_Closed_After_Result_Item_Budget()
    {
        var rule = new CapabilityRule(
            SourceCapability.Toc,
            RuleRequest.Get("/book/1"),
            [],
            List: new RuleListBinding("a.chapter", "href", "/chapter/", string.Empty));
        var source = Source.Rehydrate(
            "bounded-toc-source",
            "有界目录来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "bounded-toc-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var http = new PagingHttpClient();
        var limits = new SourceRuleExecutionLimits { MaxResultItems = 1 };
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(http, new RuleSelectorEvaluator(), limits),
            new RuleSelectorEvaluator(),
            limits);

        var entries = await adapter.GetTableOfContentsAsync("1");

        Assert.AreEqual(0, entries.Count);
    }
}
