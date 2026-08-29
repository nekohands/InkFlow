using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceRuleDslJsonTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "source-rule-dsl-v1.json");

    [TestMethod]
    public void Valid_Source_Rule_Fixture_Parses_Through_Versioned_Codec()
    {
        var result = SourceRuleDslJson.Parse(File.ReadAllText(FixturePath));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Document);
        Assert.AreEqual("1", result.Document.SchemaVersion);
        Assert.AreEqual("fixture-source", result.Document.SourceId);
        Assert.AreEqual(2, result.Document.Rules.Count);

        var search = result.Document.Rules.Single(rule => rule.Capability == SourceCapability.Search);
        Assert.AreEqual(RuleHttpMethod.Post, search.Request.Method);
        Assert.AreEqual("{query}", search.Request.Form["keyword"]);
        Assert.IsInstanceOfType<TrimTransform>(
            search.Fields[0].Transforms.OfType<TrimTransform>().Single());
        Assert.AreEqual("href", search.List!.ExternalIdAttribute);
    }

    [TestMethod]
    public void Null_Rules_Fails_Closed_Without_Throwing()
    {
        const string json = """
            {
              "schemaVersion": "1",
              "sourceId": "fixture-source",
              "rules": null
            }
            """;

        var result = SourceRuleDslJson.Parse(json);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("rules")));
    }

    [TestMethod]
    public void Null_Request_Maps_To_A_Validation_Error()
    {
        const string json = """
            {
              "schemaVersion": "1",
              "sourceId": "fixture-source",
              "rules": [
                {
                  "capability": "content",
                  "request": null,
                  "fields": [],
                  "list": null
                }
              ]
            }
            """;

        var result = SourceRuleDslJson.Parse(json);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("request")));
    }

    [TestMethod]
    public void Missing_Required_Request_Is_Rejected_By_Json_Boundary()
    {
        const string json = """
            {
              "schemaVersion": "1",
              "sourceId": "fixture-source",
              "rules": [
                {
                  "capability": "content",
                  "fields": [],
                  "list": null
                }
              ]
            }
            """;

        var result = SourceRuleDslJson.Parse(json);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Document);
        Assert.IsTrue(result.Errors.Single().Contains("source-rule-dsl-v1"));
    }

    [TestMethod]
    public void Null_Request_Dictionaries_Map_To_Validation_Errors()
    {
        const string json = """
            {
              "schemaVersion": "1",
              "sourceId": "fixture-source",
              "rules": [
                {
                  "capability": "content",
                  "request": {
                    "method": "get",
                    "pathTemplate": "/chapter",
                    "headers": null,
                    "query": null,
                    "form": null
                  },
                  "fields": [
                    {
                      "name": "content",
                      "selector": { "kind": "css", "expression": "p" },
                      "regex": null,
                      "transforms": [],
                      "attribute": null
                    }
                  ],
                  "list": null
                }
              ]
            }
            """;

        var result = SourceRuleDslJson.Parse(json);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("headers")));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("query")));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("form")));
    }

    [TestMethod]
    public void Unknown_Properties_Are_Rejected_By_The_Versioned_Boundary()
    {
        var json = File.ReadAllText(FixturePath)
            .Replace(
                "\"sourceId\": \"fixture-source\"",
                "\"sourceId\": \"fixture-source\",\n  \"unexpected\": true",
                StringComparison.Ordinal);

        var result = SourceRuleDslJson.Parse(json);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Single().Contains("source-rule-dsl-v1"));
    }

    [TestMethod]
    public void Unsupported_Transform_Kind_Is_Rejected()
    {
        var json = File.ReadAllText(FixturePath)
            .Replace("\"kind\": \"trim\"", "\"kind\": \"uppercase\"", StringComparison.Ordinal);

        var result = SourceRuleDslJson.Parse(json);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Document);
        Assert.IsTrue(result.Errors.Single().Contains("source-rule-dsl-v1"));
    }

    [TestMethod]
    public void Builtin_Linovelib_Definition_Roundtrips_Through_Codec()
    {
        var json = SourceRuleDslJson.Serialize(LinovelibSourceDefinition.BuildRuleDsl());
        var result = SourceRuleDslJson.Parse(json);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Document);
        Assert.AreEqual(LinovelibSourceDefinition.SourceId, result.Document.SourceId);
        Assert.AreEqual(4, result.Document.Rules.Count);
        Assert.IsNotNull(result.Document.Rules.Single(rule => rule.Capability == SourceCapability.Search).List);
        Assert.IsInstanceOfType<RuleSelector>(
            result.Document.Rules
                .Single(rule => rule.Capability == SourceCapability.Content)
                .Fields[0]
                .Selector);
    }

    [TestMethod]
    public void Oversized_Json_Is_Rejected_Before_Deserialization()
    {
        var result = SourceRuleDslJson.Parse(new string('x', SourceRuleDslJson.MaxJsonLength + 1));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNull(result.Document);
        Assert.IsTrue(result.Errors.Single().Contains("size limit"));
    }

    [TestMethod]
    public void Oversized_Ast_Cannot_Be_Serialized_Into_Persisted_Json()
    {
        var fields = Enumerable.Range(0, SourceRuleDslValidator.MaxFieldsPerRule)
            .Select(index => new RuleField(
                $"field-{index}",
                new RuleSelector(
                    SelectorKind.Css,
                    new string('x', SourceRuleDslValidator.MaxSelectorExpressionLength)),
                null,
                []))
            .ToArray();

        var list = new RuleListBinding("a", "href", string.Empty, string.Empty);
        var dsl = new SourceRuleDsl("1", "oversized-source", [
            new CapabilityRule(SourceCapability.Search, RuleRequest.Get("/search"), fields, list),
            new CapabilityRule(SourceCapability.BookInfo, RuleRequest.Get("/book"), fields),
            new CapabilityRule(SourceCapability.Toc, RuleRequest.Get("/toc"), fields, list),
            new CapabilityRule(SourceCapability.Content, RuleRequest.Get("/content"), fields),
            new CapabilityRule(SourceCapability.Update, RuleRequest.Get("/update"), fields),
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() => SourceRuleDslJson.Serialize(dsl));
    }

    [TestMethod]
    public void Serialize_Roundtrips_Transform_Ast_And_Uses_Canonical_Enum_Names()
    {
        var parsed = SourceRuleDslJson.Parse(File.ReadAllText(FixturePath));
        Assert.IsTrue(parsed.IsSuccess, string.Join("; ", parsed.Errors));

        var json = SourceRuleDslJson.Serialize(parsed.Document!);
        var roundtrip = SourceRuleDslJson.Parse(json);

        Assert.IsTrue(roundtrip.IsSuccess, string.Join("; ", roundtrip.Errors));
        var originalSearch = parsed.Document!.Rules.Single(rule => rule.Capability == SourceCapability.Search);
        var roundtrippedSearch = roundtrip.Document!.Rules.Single(rule => rule.Capability == SourceCapability.Search);
        Assert.AreEqual(originalSearch.Request.Method, roundtrippedSearch.Request.Method);
        Assert.AreEqual(originalSearch.Request.Form["keyword"], roundtrippedSearch.Request.Form["keyword"]);
        Assert.AreSequenceEqual(
            originalSearch.Fields[0].Transforms.Select(transform => transform.GetType()).ToArray(),
            roundtrippedSearch.Fields[0].Transforms.Select(transform => transform.GetType()).ToArray());
        Assert.AreEqual(
            ((ReplaceTransform)originalSearch.Fields[0].Transforms[1]).From,
            ((ReplaceTransform)roundtrippedSearch.Fields[0].Transforms[1]).From);
        StringAssert.Contains(json, "\"capability\": \"search\"");
        StringAssert.Contains(json, "\"kind\": \"trim\"");
    }

    [TestMethod]
    public void Serialize_Roundtrips_JsonPath_List_Binding_Metadata()
    {
        var dsl = new SourceRuleDsl(
            "1",
            "json-source",
            [new CapabilityRule(
                SourceCapability.Search,
                RuleRequest.Get("/search"),
                [],
                new RuleListBinding(
                    "$.items[*]",
                    "id",
                    string.Empty,
                    string.Empty,
                    SelectorKind.JsonPath,
                    "title"))]);

        var json = SourceRuleDslJson.Serialize(dsl);
        var result = SourceRuleDslJson.Parse(json);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual(SelectorKind.JsonPath, result.Document!.Rules[0].List!.ItemsSelectorKind);
        Assert.AreEqual("title", result.Document.Rules[0].List!.TextAttribute);
        StringAssert.Contains(json, "\"itemsSelectorKind\": \"jsonPath\"");
        StringAssert.Contains(json, "\"textAttribute\": \"title\"");
    }

    [TestMethod]
    public void Serialize_Roundtrips_Json_Next_Link_Pagination_Metadata()
    {
        var dsl = new SourceRuleDsl(
            "1",
            "json-paged-source",
            [new CapabilityRule(
                SourceCapability.Search,
                RuleRequest.Get("/search"),
                [],
                new RuleListBinding(
                    "$.items[*]",
                    "id",
                    string.Empty,
                    string.Empty,
                    SelectorKind.JsonPath,
                    "title"),
                new RulePagination(
                    new RuleSelector(SelectorKind.JsonPath, "$.next"),
                    null,
                    3))]);

        var json = SourceRuleDslJson.Serialize(dsl);
        var result = SourceRuleDslJson.Parse(json);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        var pagination = result.Document!.Rules[0].Pagination!;
        Assert.AreEqual(SelectorKind.JsonPath, pagination.NextPageSelector!.Kind);
        Assert.AreEqual("$.next", pagination.NextPageSelector.Expression);
        Assert.IsNull(pagination.NextPageAttribute);
        Assert.AreEqual(3, pagination.MaxPages);
        StringAssert.Contains(json, "\"pagination\"");
        StringAssert.Contains(json, "\"nextPageSelector\"");
    }

    [TestMethod]
    public void Serialize_Roundtrips_Page_Number_And_Cursor_Pagination_Metadata()
    {
        var pageRequest = new RuleRequest(
            RuleHttpMethod.Get,
            "/search",
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["page"] = "1" },
            new Dictionary<string, string>());
        var pageDsl = new SourceRuleDsl(
            "1",
            "page-source",
            [new CapabilityRule(
                SourceCapability.Search,
                pageRequest,
                [],
                new RuleListBinding("a.item", "href", string.Empty, string.Empty),
                new RulePagination(
                    new RuleSelector(SelectorKind.Css, "a.next"),
                    "href",
                    3)
                {
                    Mode = RulePaginationMode.PageNumber,
                    ParameterName = "page",
                    StartPage = 1,
                    PageStep = 1,
                })]);

        var pageJson = SourceRuleDslJson.Serialize(pageDsl);
        var pageResult = SourceRuleDslJson.Parse(pageJson);

        Assert.IsTrue(pageResult.IsSuccess, string.Join("; ", pageResult.Errors));
        var pagePagination = pageResult.Document!.Rules[0].Pagination!;
        Assert.AreEqual(RulePaginationMode.PageNumber, pagePagination.Mode);
        Assert.AreEqual("page", pagePagination.ParameterName);
        Assert.AreEqual(1, pagePagination.StartPage);
        Assert.AreEqual(1, pagePagination.PageStep);
        StringAssert.Contains(pageJson, "\"mode\": \"pageNumber\"");

        var cursorRequest = new RuleRequest(
            RuleHttpMethod.Get,
            "/search",
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["cursor"] = string.Empty },
            new Dictionary<string, string>());
        var cursorDsl = new SourceRuleDsl(
            "1",
            "cursor-source",
            [new CapabilityRule(
                SourceCapability.Search,
                cursorRequest,
                [],
                new RuleListBinding("$.items[*]", "id", string.Empty, string.Empty, SelectorKind.JsonPath),
                new RulePagination(MaxPages: 3)
                {
                    Mode = RulePaginationMode.Cursor,
                    ParameterName = "cursor",
                    CursorSelector = new RuleSelector(SelectorKind.JsonPath, "$.nextCursor"),
                })]);

        var cursorJson = SourceRuleDslJson.Serialize(cursorDsl);
        var cursorResult = SourceRuleDslJson.Parse(cursorJson);

        Assert.IsTrue(cursorResult.IsSuccess, string.Join("; ", cursorResult.Errors));
        var cursorPagination = cursorResult.Document!.Rules[0].Pagination!;
        Assert.AreEqual(RulePaginationMode.Cursor, cursorPagination.Mode);
        Assert.AreEqual("cursor", cursorPagination.ParameterName);
        Assert.AreEqual("$.nextCursor", cursorPagination.CursorSelector!.Expression);
        StringAssert.Contains(cursorJson, "\"cursorSelector\"");
    }

    [TestMethod]
    public void Serialize_Roundtrips_Response_Derived_Variable_Metadata()
    {
        var dsl = new SourceRuleDsl(
            "1",
            "response-variable-source",
            [new CapabilityRule(
                SourceCapability.Search,
                new RuleRequest(
                    RuleHttpMethod.Get,
                    "/search",
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>
                    {
                        ["page"] = "1",
                        ["token"] = "{token}",
                    },
                    new Dictionary<string, string>()),
                [],
                new RuleListBinding("$.items[*]", "id", string.Empty, string.Empty, SelectorKind.JsonPath),
                new RulePagination(
                    new RuleSelector(SelectorKind.JsonPath, "$.hasNext"),
                    null,
                    3)
                {
                    Mode = RulePaginationMode.Cursor,
                    ParameterName = "page",
                    CursorSelector = new RuleSelector(SelectorKind.JsonPath, "$.cursor"),
                },
                ResponseVariables: [
                    new RuleResponseVariable(
                        "token",
                        new RuleSelector(SelectorKind.JsonPath, "$.token"),
                        null,
                        [new TrimTransform()]),
                ])]);

        var json = SourceRuleDslJson.Serialize(dsl);
        var result = SourceRuleDslJson.Parse(json);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        var responseVariable = result.Document!.Rules[0].ResponseVariables!.Single();
        Assert.AreEqual("token", responseVariable.Name);
        Assert.AreEqual("$.token", responseVariable.Selector!.Expression);
        StringAssert.Contains(json, "\"responseVariables\"");
    }

    [TestMethod]
    public void Serialize_Roundtrips_Bounded_Session_Metadata_Without_Cookie_Values()
    {
        var dsl = new SourceRuleDsl(
            "1",
            "session-source",
            [new CapabilityRule(
                SourceCapability.Content,
                RuleRequest.Get("/chapter/1"),
                [new RuleField("content", new RuleSelector(SelectorKind.Css, "p"), null, [])],
                Session: new RuleSession(
                    MaxCookies: 4,
                    MaxCookieBytes: 1024,
                    MaxCookieLifetimeSeconds: 120))]);

        var json = SourceRuleDslJson.Serialize(dsl);
        var result = SourceRuleDslJson.Parse(json);

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        var session = result.Document!.Rules[0].Session!;
        Assert.AreEqual(4, session.MaxCookies);
        Assert.AreEqual(1024, session.MaxCookieBytes);
        Assert.AreEqual(120, session.MaxCookieLifetimeSeconds);
        StringAssert.Contains(json, "\"session\"");
        Assert.IsFalse(json.Contains("sid=", StringComparison.OrdinalIgnoreCase));
    }
}
