using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceRuleDslValidatorTests
{
    private static SourceRuleDsl ValidDsl() => new(
        "1",
        "example-source",
        [
            new CapabilityRule(
                SourceCapability.Search,
                RuleRequest.Get("/search?q={query}"),
                [
                    new RuleField("title", new RuleSelector(SelectorKind.Css, ".book-title"), null, []),
                    new RuleField(
                        "url",
                        new RuleSelector(SelectorKind.XPath, "//a/@href"),
                        null,
                        [new ReplaceTransform("/book/", "")]),
                ],
                List: new RuleListBinding(
                    ItemsSelector: ".result a",
                    ExternalIdAttribute: "href",
                    IdPrefixToStrip: string.Empty,
                    IdSuffixToStrip: ".html")),
        ]);

    [TestMethod]
    public void Valid_Dsl_Passes_Without_Errors()
    {
        CollectionAssert.AreEqual(Array.Empty<string>(), (System.Collections.ICollection)SourceRuleDslValidator.Validate(ValidDsl()));
    }

    [TestMethod]
    public void Null_Dsl_Is_Rejected()
    {
        Assert.IsTrue(SourceRuleDslValidator.Validate(null).Count > 0);
    }

    [TestMethod]
    public void Unsupported_SchemaVersion_Is_Rejected()
    {
        var dsl = ValidDsl() with { SchemaVersion = "99" };
        Assert.IsTrue(SourceRuleDslValidator.Validate(dsl).Any(e => e.Contains("schemaVersion")));
    }

    [TestMethod]
    public void Empty_SourceId_Is_Rejected()
    {
        var dsl = ValidDsl() with { SourceId = " " };
        Assert.IsTrue(SourceRuleDslValidator.Validate(dsl).Any(e => e.Contains("sourceId")));
    }

    [TestMethod]
    public void Whitespace_SourceId_Is_Rejected()
    {
        var dsl = ValidDsl() with { SourceId = "my source" };
        Assert.IsTrue(SourceRuleDslValidator.Validate(dsl).Any(e => e.Contains("sourceId")));
    }

    [TestMethod]
    public void Duplicate_Capability_Rule_Is_Rejected()
    {
        var dsl = ValidDsl();
        var rules = dsl.Rules.Append(dsl.Rules[0]).ToList();
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });
        Assert.IsTrue(result.Any(e => e.Contains("duplicate rule")));
    }

    [TestMethod]
    public void PathTemplate_Must_Start_With_Slash()
    {
        var dsl = ValidDsl();
        var bad = new CapabilityRule(SourceCapability.Toc, RuleRequest.Get("toc/{bookId}"), []);
        var rules = dsl.Rules.Append(bad).ToList();
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });
        Assert.IsTrue(result.Any(e => e.Contains("must start with '/'")));
    }

    [TestMethod]
    public void Malformed_PathTemplate_Placeholder_Is_Rejected()
    {
        var dsl = ValidDsl();
        var bad = new CapabilityRule(SourceCapability.Toc, RuleRequest.Get("/toc/{9bad}"), []);
        var rules = dsl.Rules.Append(bad).ToList();
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });
        Assert.IsTrue(result.Any(e => e.Contains("malformed placeholder")));
    }

    [TestMethod]
    public void Field_Without_Extraction_Source_Is_Rejected()
    {
        var dsl = ValidDsl();
        var bad = new CapabilityRule(SourceCapability.BookInfo, RuleRequest.Get("/book/{bookId}"),
            [new RuleField("author", null, null, [])]);
        var rules = dsl.Rules.Append(bad).ToList();
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });
        Assert.IsTrue(result.Any(e => e.Contains("exactly one extraction source")));
    }

    [TestMethod]
    public void Field_With_Two_Extraction_Sources_Is_Rejected()
    {
        var dsl = ValidDsl();
        var bad = new CapabilityRule(
            SourceCapability.BookInfo,
            RuleRequest.Get("/book/{bookId}"),
            [new RuleField("title", new RuleSelector(SelectorKind.Css, "h1"), new RuleRegex(".*", 100), [])]);
        var rules = dsl.Rules.Append(bad).ToList();
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });
        Assert.IsTrue(result.Any(e => e.Contains("keep exactly one")));
    }

    [TestMethod]
    public void Regex_Without_Positive_Timeout_Is_Rejected()
    {
        var dsl = ValidDsl();
        var bad = new CapabilityRule(SourceCapability.Content, RuleRequest.Get("/chapter/{chapterId}"),
            [new RuleField("body", null, new RuleRegex("<p>(.*?)</p>", 0), [])]);
        var rules = dsl.Rules.Append(bad).ToList();
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });
        Assert.IsTrue(result.Any(e => e.Contains("regex timeout")));
    }

    [TestMethod]
    public void Regex_Timeout_Above_Ceiling_Is_Rejected()
    {
        var dsl = ValidDsl();
        var bad = new CapabilityRule(SourceCapability.Content, RuleRequest.Get("/chapter/{chapterId}"),
            [new RuleField("body", null, new RuleRegex("<p>(.*?)</p>", 10_000), [])]);
        var rules = dsl.Rules.Append(bad).ToList();
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });
        Assert.IsTrue(result.Any(e => e.Contains("regex timeout")));
    }

    [TestMethod]
    public void Empty_Replace_From_Is_Rejected()
    {
        var dsl = ValidDsl();
        var bad = new CapabilityRule(SourceCapability.BookInfo, RuleRequest.Get("/book/{bookId}"),
            [new RuleField("intro", new RuleSelector(SelectorKind.Css, ".intro"), null, [new ReplaceTransform("", "x")])]);
        var rules = dsl.Rules.Append(bad).ToList();
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });
        Assert.IsTrue(result.Any(e => e.Contains("replace transform")));
    }

    [TestMethod]
    public void Duplicate_Field_Names_Are_Rejected()
    {
        var dsl = ValidDsl();
        var duplicated = dsl.Rules[0].Fields.Append(new RuleField("title", new RuleSelector(SelectorKind.Css, "h2"), null, []));
        var bad = dsl.Rules[0] with { Fields = duplicated.ToList() };
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = [bad] });
        Assert.IsTrue(result.Any(e => e.Contains("duplicate field name")));
    }

    [TestMethod]
    public void Post_Without_Form_Is_Rejected()
    {
        var dsl = ValidDsl();
        var post = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(RuleHttpMethod.Post, "/search", new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>()),
            [new RuleField("title", new RuleSelector(SelectorKind.JsonPath, "$..title"), null, [])]);
        var result = SourceRuleDslValidator.Validate(dsl with { Rules = [post] });
        Assert.IsTrue(result.Any(e => e.Contains("POST request requires")));
    }

    [TestMethod]
    public void List_Selector_Metadata_Validates_Kind_And_Text_Attribute()
    {
        var dsl = ValidDsl();
        var list = dsl.Rules[0].List! with
        {
            ItemsSelectorKind = (SelectorKind)99,
            TextAttribute = " "
        };

        var result = SourceRuleDslValidator.Validate(
            dsl with { Rules = [dsl.Rules[0] with { List = list }] });

        Assert.IsTrue(result.Any(e => e.Contains("list selector kind")));
        Assert.IsTrue(result.Any(e => e.Contains("textAttribute")));
    }

    [TestMethod]
    public void Selector_Expressions_Must_Use_Their_Declared_Root_Syntax()
    {
        var dsl = ValidDsl();
        var rules = dsl.Rules.Select(rule => rule with
        {
            Fields = [
                new RuleField(
                    "json",
                    new RuleSelector(SelectorKind.JsonPath, "items.title"),
                    null,
                    []),
                new RuleField(
                    "xpath",
                    new RuleSelector(SelectorKind.XPath, "root/item"),
                    null,
                    [])
            ],
            List = rule.List
        }).ToList();

        var result = SourceRuleDslValidator.Validate(dsl with { Rules = rules });

        Assert.IsTrue(result.Any(e => e.Contains("JSONPath selector")));
        Assert.IsTrue(result.Any(e => e.Contains("XPath selector")));
    }

    [TestMethod]
    public void Pagination_Requires_A_Search_Or_Toc_List_Binding()
    {
        var bad = new CapabilityRule(
            SourceCapability.Content,
            RuleRequest.Get("/chapter/1"),
            [new RuleField("content", new RuleSelector(SelectorKind.Css, ".content"), null, [])],
            Pagination: new RulePagination(new RuleSelector(SelectorKind.Css, "a.next")));

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "paged-source", [bad]));

        Assert.IsTrue(result.Any(e => e.Contains("Search/Toc list binding")));
    }

    [TestMethod]
    public void Pagination_MaxPages_Is_Finite()
    {
        var rule = ValidDsl().Rules[0] with
        {
            Pagination = new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                SourceRuleDslValidator.MaxPaginationPages + 1)
        };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "paged-source", [rule]));

        Assert.IsTrue(result.Any(e => e.Contains("maxPages")));
    }

    [TestMethod]
    public void Css_Pagination_Selector_Requires_A_Link_Attribute()
    {
        var rule = ValidDsl().Rules[0] with
        {
            Pagination = new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                null,
                3)
        };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "paged-source", [rule]));

        Assert.IsTrue(result.Any(e => e.Contains("CSS next-page selector requires")));
    }

    [TestMethod]
    public void Page_Number_Pagination_Requires_An_Existing_Query_Or_Form_Parameter()
    {
        var rule = ValidDsl().Rules[0] with
        {
            Pagination = new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                3)
            {
                Mode = RulePaginationMode.PageNumber,
                ParameterName = "page",
            },
        };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "paged-source", [rule]));

        Assert.IsTrue(result.Any(e => e.Contains("declared exactly once")));
    }

    [TestMethod]
    public void Page_Number_Pagination_Validates_Start_And_Step_Bounds()
    {
        var request = new RuleRequest(
            RuleHttpMethod.Get,
            "/search",
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["page"] = "1" },
            new Dictionary<string, string>());
        var rule = ValidDsl().Rules[0] with
        {
            Request = request,
            Pagination = new RulePagination(
                new RuleSelector(SelectorKind.Css, "a.next"),
                "href",
                3)
            {
                Mode = RulePaginationMode.PageNumber,
                ParameterName = "page",
                StartPage = -1,
                PageStep = 0,
            },
        };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "paged-source", [rule]));

        Assert.IsTrue(result.Any(e => e.Contains("startPage")));
        Assert.IsTrue(result.Any(e => e.Contains("pageStep")));
    }

    [TestMethod]
    public void Cursor_Pagination_Requires_A_Cursor_Selector_And_Parameter()
    {
        var rule = ValidDsl().Rules[0] with
        {
            Pagination = new RulePagination(MaxPages: 3)
            {
                Mode = RulePaginationMode.Cursor,
            },
        };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "cursor-source", [rule]));

        Assert.IsTrue(result.Any(e => e.Contains("cursorSelector")));
        Assert.IsTrue(result.Any(e => e.Contains("parameterName")));
    }

    [TestMethod]
    public void Response_Derived_Variables_Require_Page_Number_Or_Cursor_Continuation()
    {
        var rule = ValidDsl().Rules[0] with
        {
            ResponseVariables = [
                new RuleResponseVariable(
                    "token",
                    new RuleSelector(SelectorKind.JsonPath, "$.token"),
                    null,
                    []),
            ],
        };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "response-variable-source", [rule]));

        Assert.IsTrue(result.Any(error => error.Contains("page-number or cursor")));
    }

    [TestMethod]
    public void Response_Derived_Variable_Names_Must_Be_Unique_And_Bounded()
    {
        var rule = ValidDsl().Rules[0] with
        {
            Pagination = new RulePagination(
                new RuleSelector(SelectorKind.JsonPath, "$.hasNext"),
                null,
                MaxPages: 2)
            {
                Mode = RulePaginationMode.Cursor,
                ParameterName = "cursor",
                CursorSelector = new RuleSelector(SelectorKind.JsonPath, "$.cursor"),
            },
            ResponseVariables = [
                new RuleResponseVariable(
                    "token",
                    new RuleSelector(SelectorKind.JsonPath, "$.token"),
                    null,
                    []),
                new RuleResponseVariable(
                    "token",
                    new RuleSelector(SelectorKind.JsonPath, "$.other"),
                    null,
                    []),
            ],
        };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "response-variable-source", [rule]));

        Assert.IsTrue(result.Any(error => error.Contains("duplicate response variable name")));
    }

    [TestMethod]
    public void Validator_Returns_All_Violations_At_Once()
    {
        var dsl = new SourceRuleDsl("42", "", []);
        var result = SourceRuleDslValidator.Validate(dsl);
        Assert.IsTrue(result.Count >= 3, $"expected multiple violations, got: {string.Join("; ", result)}");
    }

    [TestMethod]
    public void Session_Limits_Are_Bounded()
    {
        var rule = ValidDsl().Rules[0] with
        {
            Session = new RuleSession(
                MaxCookies: SourceRuleDslValidator.MaxSessionCookies + 1,
                MaxCookieBytes: SourceRuleDslValidator.MaxSessionCookieBytes + 1,
                MaxCookieLifetimeSeconds: SourceRuleDslValidator.MaxSessionCookieLifetimeSeconds + 1),
        };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "session-source", [rule]));

        Assert.IsTrue(result.Any(error => error.Contains("maxCookies")));
        Assert.IsTrue(result.Any(error => error.Contains("maxCookieBytes")));
        Assert.IsTrue(result.Any(error => error.Contains("maxCookieLifetimeSeconds")));
    }

    [TestMethod]
    public void Static_Cookie_Request_Headers_Are_Rejected()
    {
        var request = RuleRequest.Get("/search") with
        {
            Headers = new Dictionary<string, string> { ["set-cookie"] = "sid=plaintext" },
        };
        var rule = ValidDsl().Rules[0] with { Request = request };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "session-source", [rule]));

        Assert.IsTrue(result.Any(error => error.Contains("Cookie/Set-Cookie")));
    }

    [TestMethod]
    public void Malformed_Header_Template_Placeholder_Is_Rejected()
    {
        var request = RuleRequest.Get("/search") with
        {
            Headers = new Dictionary<string, string> { ["X-Trace"] = "trace-{requestId" },
        };
        var rule = ValidDsl().Rules[0] with { Request = request };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "template-source", [rule]));

        Assert.IsTrue(result.Any(error => error.Contains("malformed placeholder")));
    }

    [TestMethod]
    public void Header_Control_Characters_Are_Rejected_At_Publish_Boundary()
    {
        var request = RuleRequest.Get("/search") with
        {
            Headers = new Dictionary<string, string> { ["X-Trace"] = "trace-\r\nX-Evil: yes" },
        };
        var rule = ValidDsl().Rules[0] with { Request = request };

        var result = SourceRuleDslValidator.Validate(
            new SourceRuleDsl("1", "template-source", [rule]));

        Assert.IsTrue(result.Any(error => error.Contains("control characters")));
    }
}
