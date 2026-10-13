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

        return Guid.TryParse(id, out var userId) ? userId : throw new UnauthorizedAccessException("Invalid user id.");
    }

    public static string GetRole(this ClaimsPrincipal user)
    {
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value.Trim().ToLowerInvariant()).Distinct().ToArray();
        if (roles.Contains(AppRoles.Admin)) return AppRoles.Admin;
        if (roles.Contains(AppRoles.Dispatcher)) return AppRoles.Dispatcher;
        // Never choose an arbitrary first role that can bypass ownership filtering.
        return roles.Length == 1 ? roles[0] : string.Empty;
    }

    public static string GetEmail(this ClaimsPrincipal user)
    {
        var email =
            user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        if (string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedAccessException("Missing email.");

        return email;
    }

    public static bool IsAdminOrDispatcher(this ClaimsPrincipal user)
    {
        var role = user.GetRole();
        return role == AppRoles.Admin || role == AppRoles.Dispatcher;
    }
}