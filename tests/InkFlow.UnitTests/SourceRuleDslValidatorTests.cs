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
                ]),
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
    public void Validator_Returns_All_Violations_At_Once()
    {
        var dsl = new SourceRuleDsl("42", "", []);
        var result = SourceRuleDslValidator.Validate(dsl);
        Assert.IsTrue(result.Count >= 3, $"expected multiple violations, got: {string.Join("; ", result)}");
    }
}
