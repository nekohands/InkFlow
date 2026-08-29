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
}
