using InkFlow.BuildingBlocks.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ResultTests
{
    [TestMethod]
    public void Success_contains_value()
    {
        var result = Result<int>.Success(42);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.Value);
        Assert.AreEqual(Error.None, result.Error);
    }
}
