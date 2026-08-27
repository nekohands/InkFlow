using System.Security.Claims;
using InkFlow.Api;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ReadingEndpointTests
{
    private static readonly Guid UserId = Guid.Parse("018f1b3a-9c0a-7b41-8a2e-0123456789ab");

    [TestMethod]
    public void User_Id_Is_Read_From_Authenticated_Subject_Only()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", UserId.ToString())],
            authenticationType: "test"));

        Assert.IsTrue(ReadingEndpointResults.TryGetUserId(principal, out var parsed));
        Assert.AreEqual(UserId, parsed);
        Assert.IsFalse(ReadingEndpointResults.TryGetUserId(new ClaimsPrincipal(), out _));
    }

    [TestMethod]
    public void Status_And_Theme_Parsers_Fail_Closed()
    {
        Assert.IsTrue(ReadingEndpointResults.TryParseShelfStatus("completed", out var status));
        Assert.AreEqual(ShelfStatus.Completed, status);
        Assert.IsFalse(ReadingEndpointResults.TryParseShelfStatus("administrator", out _));

        Assert.IsTrue(ReadingEndpointResults.TryParseTheme("sepia", out var theme));
        Assert.AreEqual(ReaderTheme.Sepia, theme);
        Assert.IsFalse(ReadingEndpointResults.TryParseTheme("neon", out _));
    }
}
