using InkFlow.BuildingBlocks.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class IntegrationSmokeTests
{
    [TestMethod]
    public void Persistence_assembly_is_loadable()
    {
        var assemblyName = typeof(PersistenceAssembly).Assembly.GetName().Name;

        Assert.AreEqual("InkFlow.BuildingBlocks.Persistence", assemblyName);
    }
}
