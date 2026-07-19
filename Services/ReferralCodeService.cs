using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class ReferralCodeService
{
    private readonly AppDbContext _context;

    public ReferralCodeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUniqueCodeAsync(
        string fullName,
        CancellationToken cancellationToken = default)
    {
        var prefix = CreatePrefix(fullName);

        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var randomPart = GenerateRandomPart(6);
            var referralCode = $"{prefix}-{randomPart}";

            var exists = await _context.Users
                .AnyAsync(
                    user => user.ReferralCode == referralCode,
                    cancellationToken
                );

            if (!exists)
            {
                return referralCode;
            }
        }

        // Extremely unlikely fallback in case random codes collide.
        return $"{prefix}-{Guid.NewGuid():N}"
            .ToUpperInvariant()[..Math.Min(prefix.Length + 13, 20)];
    }

    private static string CreatePrefix(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "ALPHA";
        }

        var cleanedName = Regex.Replace(
            fullName.Trim().ToUpperInvariant(),
            @"[^A-Z0-9\s]",
            ""
        );

        var firstName = cleanedName
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries
            )
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstName))
        {
            return "ALPHA";
        }

        return firstName.Length > 8
            ? firstName[..8]
            : firstName;
    }

    private static string GenerateRandomPart(int length)
    {
        const string characters =
            "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        return string.Concat(
            Enumerable.Range(0, length)
                .Select(_ =>
                    characters[
                        Random.Shared.Next(characters.Length)
                    ])
        );
    }
}