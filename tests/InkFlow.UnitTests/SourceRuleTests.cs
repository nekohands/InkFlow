using InkFlow.Modules.Sources.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceRuleTests
{
    [TestMethod]
    public void Validator_accepts_complete_v1_rule()
    {
        var rule = CreateRule();

        var errors = new SourceRuleValidator().Validate(rule);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void Validator_rejects_capability_without_operation()
    {
        var rule = CreateRule() with
        {
            Capabilities = SourceCapability.Search | SourceCapability.Content
        };

        var errors = new SourceRuleValidator().Validate(rule);

        Assert.IsTrue(errors.Any(error => error.Code == "RULE_OPERATION_REQUIRED" && error.Path == "content"));
    }

    [TestMethod]
    public void Request_compiler_expands_templates_and_query_parameters()
    {
        var request = new RuleAdapter().BuildRequest(
            CreateRule(),
            SourceOperation.Search,
            new Dictionary<string, string> { ["keyword"] = "斗破 苍穹" });

        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("https://example.com/search?q=%E6%96%97%E7%A0%B4%20%E8%8B%8D%E7%A9%B9", request.Uri.AbsoluteUri);
    }

    [TestMethod]
    public void Css_fixture_extracts_multiple_rows_and_attributes()
    {
        const string html = """
            <ul>
              <li class="book"><a href="/book/1">  Book One </a><span class="author">Alice</span></li>
              <li class="book"><a href="/book/2">Book Two</a><span class="author">Bob</span></li>
            </ul>
            """;

        var result = new RuleAdapter().ParseResponse(CreateRule(), SourceOperation.Search, html);

        Assert.HasCount(2, result.Rows);
        Assert.AreEqual("Book One", result.Rows[0]["title"]);
        Assert.AreEqual("/book/1", result.Rows[0]["url"]);
        Assert.AreEqual("Bob", result.Rows[1]["author"]);
    }

    [TestMethod]
    public void XPath_fixture_extracts_values()
    {
        const string html = "<article><h1>Chapter 1</h1><div id='content'>Hello <b>world</b></div></article>";
        var operation = new SourceOperationRule(
            new RequestRule("GET", "/chapter/1"),
            new Dictionary<string, ExtractionFieldRule>
            {
                ["title"] = new(SelectorKind.XPath, "//h1", Transforms: [new(TransformKind.Trim)]),
                ["content"] = new(SelectorKind.XPath, "//div[@id='content']", Transforms: [new(TransformKind.CollapseWhitespace)])
            });
        var rule = CreateRule() with
        {
            Capabilities = SourceCapability.Content,
            Search = null,
            Content = operation
        };

        var result = new RuleAdapter().ParseResponse(rule, SourceOperation.Content, html);

        Assert.AreEqual("Chapter 1", result.Rows[0]["title"]);
        Assert.AreEqual("Hello world", result.Rows[0]["content"]);
    }

    [TestMethod]
    public void JsonPath_fixture_extracts_multiple_rows()
    {
        const string json = """
            {"books":[{"title":"A","author":"AA"},{"title":"B","author":"BB"}]}
            """;
        var operation = new SourceOperationRule(
            new RequestRule("GET", "/api/search"),
            new Dictionary<string, ExtractionFieldRule>
            {
                ["title"] = new(SelectorKind.JsonPath, "$.books[*].title"),
                ["author"] = new(SelectorKind.JsonPath, "$.books[*].author")
            },
            Multiple: true);
        var rule = CreateRule() with { Search = operation };

        var result = new RuleAdapter().ParseResponse(rule, SourceOperation.Search, json);

        Assert.HasCount(2, result.Rows);
        Assert.AreEqual("A", result.Rows[0]["title"]);
        Assert.AreEqual("BB", result.Rows[1]["author"]);
    }

    [TestMethod]
    public void Rule_json_round_trips_string_enums()
    {
        var json = SourceRuleJson.Serialize(CreateRule());
        var restored = SourceRuleJson.Deserialize(json);

        Assert.AreEqual(SourceCapability.Search, restored.Capabilities);
        Assert.AreEqual(SelectorKind.Css, restored.Search!.Fields["title"].Kind);
    }

    private static SourceRuleDocument CreateRule() => new(
        SchemaVersion: 1,
        Name: "fixture-source",
        BaseUrl: "https://example.com/",
        Capabilities: SourceCapability.Search,
        Budget: new RuleExecutionBudget(),
        Search: new SourceOperationRule(
            new RequestRule(
                "GET",
                "/search",
                Query: new Dictionary<string, string> { ["q"] = "{{keyword}}" }),
            new Dictionary<string, ExtractionFieldRule>
            {
                ["title"] = new(SelectorKind.Css, "li.book a", Transforms: [new(TransformKind.Trim)]),
                ["url"] = new(SelectorKind.Css, "li.book a", Attribute: "href"),
                ["author"] = new(SelectorKind.Css, "li.book .author", Transforms: [new(TransformKind.Trim)])
            },
            Multiple: true));
}
