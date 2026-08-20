using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class IntegrationSmokeTests
{
    [TestMethod]
    public void Integration_test_project_is_discoverable() => Assert.IsTrue(true);
}
