using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

/// <summary>骨架阶段的单元测试管线守卫。</summary>
[TestClass]
public sealed class SkeletonSmokeTests
{
    [TestMethod]
    public void TestPipeline_IsOperative()
    {
        Assert.AreEqual("InkFlow", string.Concat("Ink", "Flow"));
    }
}
