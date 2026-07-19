using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class ReferralCodeService
{
    private const string Characters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly AppDbContext _context;

    public ReferralCodeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUniqueCodeAsync(
        string fullName,
        CancellationToken cancellationToken = default)
    {
        var cleanName = Regex.Replace(
            fullName.ToUpperInvariant(),
            "[^A-Z0-9]",
            string.Empty
        );

        var prefix = string.IsNullOrWhiteSpace(cleanName)
            ? "ALPHA"
            : cleanName[..Math.Min(cleanName.Length, 6)];

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = GenerateRandomSuffix(6);
            var code = $"{prefix}-{suffix}";

            var exists = await _context.Users
                .AnyAsync(
                    user => user.ReferralCode == code,
                    cancellationToken
                );

            if (!exists)
            {
                return code;
            }
        }

        return $"ALPHA-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
    }

    private static string GenerateRandomSuffix(int length)
    {
        var result = new char[length];

        for (var index = 0; index < length; index++)
        {
            result[index] =
                Characters[RandomNumberGenerator.GetInt32(Characters.Length)];
        }

        return new string(result);
    }
}