using System.Text.Json;
using InkFlow.Api;

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
}
