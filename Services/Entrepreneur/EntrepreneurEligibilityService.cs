using Alpha.API.Data;
using Alpha.API.Security;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services.Entrepreneur;

public class EntrepreneurEligibilityService
{
    private readonly AppDbContext _context;

    public EntrepreneurEligibilityService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Eligible, string? Reason)>
        CheckAsync(
            Guid recruitedUserId,
            Guid entrepreneurUserId,
            Guid orderId,
            string providerRole,
            CancellationToken cancellationToken = default)
    {
        var normalizedRole =
            providerRole.Trim().ToLowerInvariant();

        var allowedRoles =
            new[]
            {
                EntrepreneurRoles.Driver,
                EntrepreneurRoles.Mechanic,
                EntrepreneurRoles.Supplier
            };

        if (!allowedRoles.Contains(normalizedRole))
        {
            return (
                false,
                "Provider role is not eligible for the Entrepreneur Network."
            );
        }

        // -----------------------------------------
        // Direct referral only
        // -----------------------------------------

        var referral =
            await _context.EntrepreneurReferrals
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.RecruitedUserId ==
                            recruitedUserId
                        &&
                        x.EntrepreneurUserId ==
                            entrepreneurUserId
                        &&
                        x.IsDirectReferral
                        &&
                        x.EndedAt == null,
                    cancellationToken);

        if (referral == null)
        {
            return (
                false,
                "No active direct Entrepreneur referral exists."
            );
        }

        if (!string.Equals(
                referral.ReferralStatus,
                "active",
                StringComparison.OrdinalIgnoreCase))
        {
            return (
                false,
                "Referral relationship is not active."
            );
        }

        // -----------------------------------------
        // Recruited user must exist
        // -----------------------------------------

        var user =
            await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == recruitedUserId,
                    cancellationToken);

        if (user == null)
        {
            return (
                false,
                "Recruited provider does not exist."
            );
        }

        // -----------------------------------------
        // Operational role MUST be ACTIVE
        // -----------------------------------------

        var roleIsActive =
            await _context.UserRoles
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.UserId ==
                            recruitedUserId
                        &&
                        x.RoleKey ==
                            normalizedRole
                        &&
                        x.Status ==
                            "active",
                    cancellationToken);

        if (!roleIsActive)
        {
            return (
                false,
                "The recruited provider role is not active."
            );
        }

        return (true, null);
    }
}