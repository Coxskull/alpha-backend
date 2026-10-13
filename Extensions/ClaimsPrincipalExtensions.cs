using System;
using System.Security.Claims;

namespace Alpha.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var value =
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.FindFirst("user_id")?.Value;

            if (!Guid.TryParse(value, out var userId))
            {
                throw new UnauthorizedAccessException("User ID claim is missing or invalid.");
            }

            return userId;
        }

        public static string? GetRole(this ClaimsPrincipal user)
        {
            return Alpha.API.Security.AuthUser.GetRole(user);
        }
    }
}