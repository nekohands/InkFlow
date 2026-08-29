using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Infrastructure;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class RuleSelectorEvaluatorTests
{
    [TestMethod]
    public void JsonPath_EvaluateFirst_Reads_A_Nested_Scalar()
    {
        var evaluator = new RuleSelectorEvaluator();

        var value = evaluator.EvaluateFirst(
            """
            {"data":{"book":{"title":"星河入梦"}}}
            """,
            new RuleSelector(SelectorKind.JsonPath, "$.data.book.title"));

        Assert.AreEqual("星河入梦", value);
    }

    [TestMethod]
    public void XPath_EvaluateFirst_Reads_Element_Text_And_Attribute()
    {
        var evaluator = new RuleSelectorEvaluator();
        const string document = "<root><book id=\"42\"><title>星河入梦</title></book></root>";

        var title = evaluator.EvaluateFirst(
            document,
            new RuleSelector(SelectorKind.XPath, "/root/book/title"));
        var id = evaluator.EvaluateFirst(
            document,
            new RuleSelector(SelectorKind.XPath, "/root/book"),
            "id");

        Assert.AreEqual("星河入梦", title);
        Assert.AreEqual("42", id);
    }

    [TestMethod]
    public void JsonPath_SelectAll_Exposes_Object_Properties_For_List_Binding()
    {
        var evaluator = new RuleSelectorEvaluator();

        var items = evaluator.SelectAll(
            """
            {"items":[{"id":"book-1","title":"第一本"},{"id":"book-2","title":"第二本"}]}
            """,
            new RuleSelector(SelectorKind.JsonPath, "$.items[*]"));

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual("第一本", items[0].TextContent);
        Assert.AreEqual("book-1", items[0].Attributes["id"]);
        Assert.AreEqual("第二本", items[1].TextContent);
    }

    [TestMethod]
    public void XPath_SelectAll_Returns_Element_Text_And_Attributes()
    {
        var evaluator = new RuleSelectorEvaluator();

        var items = evaluator.SelectAll(
            "<root><item id=\"1\">第一本</item><item id=\"2\">第二本</item></root>",
            new RuleSelector(SelectorKind.XPath, "/root/item"));

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual("第一本", items[0].TextContent);
        Assert.AreEqual("1", items[0].Attributes["id"]);
        Assert.AreEqual("第二本", items[1].TextContent);
    }

    [TestMethod]
    public async Task Rule_Based_Search_Uses_JsonPath_List_Binding()
    {
        var source = Source.Rehydrate(
            "json-source",
            "JSON 来源",
            "https://books.example.com",
            new SourceRuleDsl(
                "1",
                "json-source",
                [new CapabilityRule(
                    SourceCapability.Search,
                    RuleRequest.Get("/search?q={key}"),
                    [],
                    new RuleListBinding(
                        "$.items[*]",
                        "id",
                        string.Empty,
                        string.Empty,
                        SelectorKind.JsonPath,
                        "title"))]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var selector = new RuleSelectorEvaluator();
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(new JsonFixtureHttpClient(), selector),
            selector);

        var results = await adapter.SearchAsync("星河");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("1", results[0].ExternalBookId);
        Assert.AreEqual("第一本", results[0].Title);
        Assert.AreEqual("2", results[1].ExternalBookId);
        Assert.AreEqual("第二本", results[1].Title);
    }

    [TestMethod]
    public void JsonPath_Unsupported_Expression_Fails_Closed()
    {
        var evaluator = new RuleSelectorEvaluator();

        var value = evaluator.EvaluateFirst(
            "{\"items\":[{\"id\":1}]}",
            new RuleSelector(SelectorKind.JsonPath, "$.items[?(@.id == 1)].id"));

        Assert.IsNull(value);
        Assert.AreEqual(
            0,
            evaluator.SelectAll(
                "{\"items\":[{\"id\":1}]}",
                new RuleSelector(SelectorKind.JsonPath, "$.items[?(@.id == 1)]")).Count);
    }

    [TestMethod]
    public void XPath_Dtd_Input_Fails_Closed()
    {
        var evaluator = new RuleSelectorEvaluator();
        const string document = "<!DOCTYPE root [<!ENTITY external SYSTEM \"file:///secret\">]><root>&external;</root>";

        var value = evaluator.EvaluateFirst(
            document,
            new RuleSelector(SelectorKind.XPath, "/root"));

        Assert.IsNull(value);
    }

    [TestMethod]
    public void Invalid_Css_Selector_Fails_Closed()
    {
        var evaluator = new RuleSelectorEvaluator();

        var value = evaluator.EvaluateFirst(
            "<root><item>内容</item></root>",
            new RuleSelector(SelectorKind.Css, "["));

        Assert.IsNull(value);
        Assert.AreEqual(
            0,
            evaluator.SelectAll(
                "<root><item>内容</item></root>",
                new RuleSelector(SelectorKind.Css, "[")).Count);
    }

    [TestMethod]
    public void XPath_Fallback_Handles_Common_HTML_Predicates()
    {
        var evaluator = new RuleSelectorEvaluator();
        const string document = "<html><head><meta charset=\"utf-8\"></head><body><div class=\"book\"><a href=\"/1\">第一本</a></div></body></html>";

        var title = evaluator.EvaluateFirst(
            document,
            new RuleSelector(SelectorKind.XPath, "//div[@class='book']/a"));
        var href = evaluator.EvaluateFirst(
            document,
            new RuleSelector(SelectorKind.XPath, "//div[@class='book']/a"),
            "href");
        var terminalHref = evaluator.EvaluateFirst(
            document,
            new RuleSelector(SelectorKind.XPath, "//div[contains(@class, 'boo')]/a/@href"));
        var links = evaluator.SelectAll(
            document,
            new RuleSelector(SelectorKind.XPath, "//div[@class='book']/a"));

        Assert.AreEqual("第一本", title);
        Assert.AreEqual("/1", href);
        Assert.AreEqual("/1", terminalHref);
        Assert.AreEqual(1, links.Count);
        Assert.AreEqual("第一本", links[0].TextContent);
    }

    private sealed class JsonFixtureHttpClient : ISourceHttpClient
    {
        public Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SourceHttpResponse(
                200,
                "{\"items\":[{\"id\":\"1\",\"title\":\"第一本\"},{\"id\":\"2\",\"title\":\"第二本\"}]}"));
    }
}
