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
}
