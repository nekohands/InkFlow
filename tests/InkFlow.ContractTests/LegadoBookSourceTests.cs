using System.Text.Json;
using InkFlow.Modules.Legado;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ContractTests;

[TestClass]
public sealed class LegadoBookSourceTests
{
    [TestMethod]
    public void Official_rule_points_only_to_stable_inkflow_api()
    {
        var rule = new LegadoBookSourceGenerator().Generate(new Uri("https://reader.example.com"));
        var json = JsonSerializer.Serialize(rule);

        StringAssert.Contains(json, "https://reader.example.com/api/legado/v1/search");
        StringAssert.Contains(json, "ruleSearch");
        StringAssert.Contains(json, "ruleBookInfo");
        StringAssert.Contains(json, "ruleToc");
        StringAssert.Contains(json, "ruleContent");
        Assert.IsFalse(json.Contains("third-party", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Official_rule_uses_json_contract_fields()
    {
        var rule = new LegadoBookSourceGenerator().Generate(new Uri("https://inkflow.example"));
        Assert.AreEqual("$[*]", rule.RuleSearch.BookList);
        Assert.AreEqual("$.tocUrl", rule.RuleBookInfo.TocUrl);
        Assert.AreEqual("$.url", rule.RuleToc.ChapterUrl);
        Assert.AreEqual("$.content", rule.RuleContent.Content);
    }
}
