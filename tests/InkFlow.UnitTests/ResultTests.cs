using InkFlow.BuildingBlocks.Application;
using InkFlow.BuildingBlocks.Domain;
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
        Assert.IsFalse(result.IsFailure);
        Assert.AreEqual(42, result.Value);
        Assert.AreEqual(Error.None, result.Error);
    }

    [TestMethod]
    public void Failure_contains_error()
    {
        var error = new Error("BOOK_NOT_FOUND", "Book was not found");
        var result = Result<int>.Failure(error);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(error, result.Error);
    }

    [TestMethod]
    public void Uuid7_generates_version_seven_guids()
    {
        var value = Uuid7.New();

        Assert.AreEqual(7, value.Version);
    }
}
