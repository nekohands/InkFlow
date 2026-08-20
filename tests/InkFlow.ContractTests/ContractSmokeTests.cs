using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ContractTests;

[TestClass]
public sealed class ContractSmokeTests
{
    [TestMethod]
    public void Legado_contract_test_project_is_discoverable() => Assert.IsNotNull(typeof(InkFlow.Modules.Legado.LegadoModule));
}
