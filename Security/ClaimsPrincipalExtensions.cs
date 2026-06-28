// Security/ClaimsPrincipalExtensions.cs

using System;
using System.Security.Claims;

namespace Alpha.API.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value =
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue("sub") ??
            user.FindFirstValue("userId") ??
            user.FindFirstValue("id");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static string? GetEmail(this ClaimsPrincipal user)
    {
        return
            user.FindFirstValue(ClaimTypes.Email) ??
            user.FindFirstValue("email");
    }
}