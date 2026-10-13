using Alpha.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace Alpha.API.Security;

public static class ActiveTokenValidator
{
    public static async Task Validate(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        { context.Fail("Invalid identity."); return; }
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId,
            context.HttpContext.RequestAborted);
        if (user == null || !user.IsActive) { context.Fail("Inactive identity."); return; }
        var memberships = await db.UserRoles.AsNoTracking().Where(r => r.UserId == userId)
            .ToListAsync(context.HttpContext.RequestAborted);
        var active = ActiveRoles.Select(user.Role, memberships);
        var claimed = context.Principal!.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (claimed.Length == 0 || claimed.Any(r => !active.Contains(r, StringComparer.Ordinal)))
            context.Fail("Inactive role.");
    }
}
