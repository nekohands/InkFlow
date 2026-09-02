using System.Text.Json;
using InkFlow.Api;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using Microsoft.AspNetCore.Http;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class AuthEndpointsTests
{
    [TestMethod]
    public void Refresh_Request_Uses_Snake_Case_Json_Contract()
    {
        var request = JsonSerializer.Deserialize<RefreshRequest>(
            "{\"refresh_token\":\"opaque-refresh-token\"}");

        Assert.IsNotNull(request);
        Assert.AreEqual("opaque-refresh-token", request.RefreshToken);
    }

    [TestMethod]
    public void Account_Profile_Response_Uses_Stable_Readable_Fields()
    {
        var profile = new IdentityProfile(
            Guid.Parse("01908d2a-2d44-7b3b-9ec2-123456789abc"),
            "reader@example.com",
            "墨客",
            UserRole.Reader,
            UserStatus.Active,
            DateTimeOffset.Parse("2026-08-28T12:00:00Z"),
            DateTimeOffset.Parse("2026-08-28T12:00:00Z"));

        var response = AccountEndpointResults.ToResponse(profile);

        Assert.AreEqual("墨客", response.DisplayName);
        Assert.AreEqual("Reader", response.Role);
        Assert.AreEqual("Active", response.Status);
    }

    [TestMethod]
    public void Password_Change_Result_Does_Not_Render_A_Secret()
    {
        var result = AccountEndpointResults.FromPasswordChange(
            new PasswordChangeOperationResult(PasswordChangeResultStatus.InvalidCredentials));

        Assert.AreEqual(StatusCodes.Status401Unauthorized, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.IsFalse(result.ToString()!.Contains("password", StringComparison.OrdinalIgnoreCase));
    }
}
