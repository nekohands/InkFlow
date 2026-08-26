using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceAggregateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 17, 0, 0, TimeSpan.Zero);

    private static SourceRuleDsl ValidDsl() => new(
        "1", "example-source",
        [
            new CapabilityRule(
                SourceCapability.Search,
                RuleRequest.Get("/search?q={query}"),
                [new RuleField("title", new RuleSelector(SelectorKind.Css, "h1"), null, [])]),
        ]);

    [TestMethod]
    public void Internal_BaseUrl_Is_Rejected_At_Creation()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => Source.Create("s", "内网来源", "http://127.0.0.1:8080", T0));
        Assert.ThrowsExactly<ArgumentException>(
            () => Source.Create("s", "元数据来源", "http://169.254.169.254/", T0));
    }

    [TestMethod]
    public void Public_Https_BaseUrl_Is_Accepted()
    {
        var source = Source.Create("example-source", "示例", "https://books.example.com", T0);
        Assert.AreEqual("example-source", source.Id);
        Assert.IsNull(source.RuleDsl);
    }

    [TestMethod]
    public void Invalid_Rule_Dsl_Is_Rejected_By_Aggregate()
    {
        var source = Source.Create("example-source", "示例", "https://books.example.com", T0);
        var invalid = ValidDsl() with { SchemaVersion = "99" };

        Assert.ThrowsExactly<InvalidOperationException>(() => source.UpdateRuleDsl(invalid, T0));
        Assert.IsNull(source.RuleDsl, "被拒绝的文档不得进入聚合");
    }

    [TestMethod]
    public void UpdateRuleDsl_Installs_Valid_Document_And_FindRule_Resolves_Capability()
    {
        var source = Source.Create("example-source", "示例", "https://books.example.com", T0);
        source.UpdateRuleDsl(ValidDsl(), T0);

        Assert.IsNotNull(source.RuleDsl);
        Assert.IsNotNull(source.FindRule(SourceCapability.Search));
        Assert.IsNull(source.FindRule(SourceCapability.Toc), "未声明的能力应返回 null");
    }
}
