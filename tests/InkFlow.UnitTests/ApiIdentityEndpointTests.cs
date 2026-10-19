using System.Security.Claims;
using InkFlow.Api;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ApiIdentityEndpointTests
{
    [TestMethod]
    public void TryGetIdentity_Parses_Subject_And_Role()
    {
        var userId = Guid.CreateVersion7();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString("D")),
            new Claim(ClaimTypes.Role, UserRole.Operator.ToString()),
        ]));

        Assert.IsTrue(ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var parsedUserId,
            out var role));
        Assert.AreEqual(userId, parsedUserId);
        Assert.AreEqual(UserRole.Operator, role);
    }

    [TestMethod]
    public void TryGetIdentity_Rejects_Missing_Or_Invalid_Claims()
    {
        var userId = Guid.CreateVersion7();

        Assert.IsFalse(ResourcePermissionEndpointResults.TryGetIdentity(
            new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", userId.ToString("D"))])),
            out _,
            out _));
        Assert.IsFalse(ResourcePermissionEndpointResults.TryGetIdentity(
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", Guid.Empty.ToString("D")),
                new Claim(ClaimTypes.Role, UserRole.Operator.ToString()),
            ])),
            out _,
            out _));
        Assert.IsFalse(ResourcePermissionEndpointResults.TryGetIdentity(
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString("D")),
                new Claim(ClaimTypes.Role, "unknown"),
            ])),
            out _,
            out _));
    }

    [TestMethod]
    public void TryGetIdentity_Preserves_Standard_Role_Claim_Precedence()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", Guid.CreateVersion7().ToString("D")),
            new Claim(ClaimTypes.Role, "unknown"),
            new Claim("role", UserRole.Administrator.ToString()),
        ]));

        Assert.IsFalse(ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out _,
            out _));
    }
}
