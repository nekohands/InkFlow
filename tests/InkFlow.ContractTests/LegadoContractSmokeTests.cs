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
}
