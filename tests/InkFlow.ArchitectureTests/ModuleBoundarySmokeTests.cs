using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ArchitectureTests;

/// <summary>
/// 边界守卫：确认业务模块程序集均可解析加载。
/// 真正的分层约束检查（NetArchTest 规则）将在模块出现实际类型后补充。
/// </summary>
[TestClass]
public sealed class ModuleBoundarySmokeTests
{
    private static readonly string[] ModuleAssemblies =
    [
        "InkFlow.Modules.Identity",
        "InkFlow.Modules.Library",
        "InkFlow.Modules.Sources",
        "InkFlow.Modules.Crawling",
        "InkFlow.Modules.Content",
        "InkFlow.Modules.Reading",
        "InkFlow.Modules.Search",
        "InkFlow.Modules.Legado",
        "InkFlow.Modules.Developers",
        "InkFlow.Modules.Billing",
        "InkFlow.Modules.Operations",
    ];

    [TestMethod]
    public void All_Module_Assemblies_Are_Loadable()
    {
        foreach (var assemblyName in ModuleAssemblies)
        {
            var loaded = System.Reflection.Assembly.Load(assemblyName);
            Assert.IsFalse(loaded.IsDynamic, $"{assemblyName} 应为普通编译程序集");
        }
    }
}
