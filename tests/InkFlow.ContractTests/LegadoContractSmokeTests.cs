using System.Text.Json;
using InkFlow.Modules.Legado.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ContractTests;

/// <summary>
/// 契约测试管线守卫：确认 Legado 兼容层程序集可解析加载。
/// 真实的 Legado 书源契约用例将在 Source Runtime 阶段落地。
/// </summary>
[TestClass]
public sealed class LegadoContractSmokeTests
{
    [TestMethod]
    public void Legado_Module_Assembly_Is_Loadable()
    {
        var loaded = System.Reflection.Assembly.Load("InkFlow.Modules.Legado");
        Assert.IsFalse(loaded.IsDynamic);
    }

    [TestMethod]
    public void Personal_Book_Source_Manifest_Uses_Header_Authentication()
    {
        const string rawToken = "lf_lgd_contract-token";
        using var document = JsonDocument.Parse(
            LegadoBookSourceManifest.Generate("https://inkflow.example.com", rawToken));

        var root = document.RootElement;
        Assert.AreEqual(
            "https://inkflow.example.com/api/legado/v1/personal/search?q={{key}}",
            root.GetProperty("searchUrl").GetString());
        Assert.IsFalse(root.GetProperty("searchUrl").GetString()!.Contains(rawToken, StringComparison.Ordinal));

        using var header = JsonDocument.Parse(root.GetProperty("header").GetString()!);
        Assert.AreEqual(
            rawToken,
            header.RootElement.GetProperty(LegadoBookSourceManifest.PersonalTokenHeader).GetString());
    }
}
