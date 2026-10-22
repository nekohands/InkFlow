using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ArchitectureTests;

/// <summary>
/// 边界守卫：确认业务模块程序集均可解析加载。
/// 类型依赖和项目图约束由 ModuleBoundaryTests 验证。
/// </summary>
[TestClass]
public sealed class ModuleBoundarySmokeTests
{
    [TestMethod]
    public void All_Module_Assemblies_Are_Loadable()
    {
        foreach (var module in ModuleDependencyPolicy.AllowedModules.Keys)
        {
            var assemblyName = ModuleDependencyPolicy.ModulePrefix + module;
            var loaded = System.Reflection.Assembly.Load(assemblyName);
            Assert.IsFalse(loaded.IsDynamic, $"{assemblyName} 应为普通编译程序集");
        }
    }
}
