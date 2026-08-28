using System.Security.Claims;
using InkFlow.Api;
using Microsoft.AspNetCore.Http;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class PrivateLibraryEndpointTests
{
    private static readonly Guid UserId = Guid.Parse("01908d2a-2d44-7b3b-9ec2-123456789abc");

    [TestMethod]
    public void User_Id_Is_Read_From_Subject_Only()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", UserId.ToString())],
            authenticationType: "test"));

        Assert.IsTrue(PrivateLibraryEndpointResults.TryGetUserId(principal, out var parsed));
        Assert.AreEqual(UserId, parsed);
        Assert.IsFalse(PrivateLibraryEndpointResults.TryGetUserId(new ClaimsPrincipal(), out _));
    }

    [TestMethod]
    public void Invalid_Operation_Is_Mapped_To_Bad_Request()
    {
        var result = PrivateLibraryEndpointResults.FromOperation(
            new InkFlow.Modules.Library.Application.PrivateLibraryOperationResult<string>(
                InkFlow.Modules.Library.Application.PrivateLibraryResultStatus.InvalidRequest,
                null));

        Assert.AreEqual(StatusCodes.Status400BadRequest, ((IStatusCodeHttpResult)result).StatusCode);
    }
}
