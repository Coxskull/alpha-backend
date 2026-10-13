using Alpha.API.Models;
namespace Alpha.API.Security;

public static class ActiveRoles
{
    public static List<string> Select(string legacyRole, IReadOnlyCollection<UserRole> memberships)
    {
        // Legacy fallback is valid only for an account with no membership records at all.
        var roles = memberships.Count == 0 ? new[] { legacyRole }
            : memberships.Where(m => m.Status == "active").Select(m => m.RoleKey);
        return roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal).OrderBy(r => r).ToList();
    }
}
