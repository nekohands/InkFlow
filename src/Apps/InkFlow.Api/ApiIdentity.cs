using System.Security.Claims;

namespace InkFlow.Api;

internal static class ApiIdentity
{
    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        return Guid.TryParse(principal.FindFirstValue("sub"), out userId) &&
               userId != Guid.Empty;
    }
}
