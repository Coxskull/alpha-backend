using System;
using System.Security.Claims;

namespace Alpha.API.Security;

public static class AuthUser
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(id))
            throw new UnauthorizedAccessException("Missing user id.");

        return Guid.Parse(id);
    }

    public static string GetRole(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role) ?? "";
    }

    public static bool IsAdminOrDispatcher(this ClaimsPrincipal user)
    {
        var role = user.GetRole();

        return role == AppRoles.Admin || role == AppRoles.Dispatcher;
    }
}