using System.Security.Claims;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Api;

internal static class ApiIdentity
{
    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        userId = Guid.Empty;
        return Guid.TryParse(principal.FindFirstValue("sub"), out userId) &&
               userId != Guid.Empty;
    }

    public static bool TryGetIdentity(
        ClaimsPrincipal principal,
        out Guid userId,
        out UserRole role)
    {
        role = default;
        if (!TryGetUserId(principal, out userId))
        {
            return false;
        }

        var rawRole = principal.FindFirstValue(ClaimTypes.Role) ??
                      principal.FindFirstValue("role");
        return Enum.TryParse(rawRole, ignoreCase: true, out role) &&
               Enum.IsDefined(role);
    }
}
