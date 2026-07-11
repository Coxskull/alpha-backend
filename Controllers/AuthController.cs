using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(
        AppDbContext context,
        IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var emailExists = await _context.Users
            .AnyAsync(user => user.Email.ToLower() == email);

        if (emailExists)
        {
            return Conflict("Email already exists.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = dto.FullName.Trim(),
                Email = email,
                Phone = dto.Phone?.Trim(),

                // Never trust a role provided by the public registration page.
                Role = "customer",

                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            // Use the same ID for users and customers.
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO customers (
                id,
                full_name,
                email,
                phone
            )
            VALUES (
                {user.Id},
                {user.FullName},
                {user.Email},
                {user.Phone}
            )
            ON CONFLICT (id) DO NOTHING
        ");

            await transaction.CommitAsync();

            return Ok(new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _context.Users
    .FirstOrDefaultAsync(user =>
        user.Email.ToLower() ==
            dto.Email.Trim().ToLower() &&
        user.IsActive);

        if (user == null)
            return Unauthorized("Invalid email or password.");

        var validPassword =
            BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);

        if (!validPassword)
            return Unauthorized("Invalid email or password.");

        var token = GenerateToken(user);

        Guid? supplierId = null;

        if (user.Role == "supplier" || user.Role == "provider")
        {
            supplierId = await _context.Suppliers
                .Where(s => s.UserId == user.Id)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();
        }

        return Ok(new
        {
            token,
            user = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                SupplierId = supplierId
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue(ClaimTypes.Email),
            role = User.FindFirstValue(ClaimTypes.Role)
        });
    }

    private string GenerateToken(User user)
    {
        var jwtKey =
            _configuration["Jwt:Key"] ??
            throw new Exception("JWT key is missing.");

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        );

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
    ForgotPasswordDto dto)
    {
        var genericResponse = new
        {
            message =
                "If the email belongs to a customer account, " +
                "a reset link has been sent."
        };

        var email = dto.Email
            .Trim()
            .ToLowerInvariant();

        // Customer reset only
        var user = await _context.Users
            .FirstOrDefaultAsync(user =>
                user.Email.ToLower() == email &&
                user.Role == "customer");

        // Do not reveal whether the account exists.
        if (user == null)
        {
            return Ok(genericResponse);
        }

        // Invalidate previous unused tokens.
        var oldTokens = await _context.PasswordResetTokens
            .Where(token =>
                token.UserId == user.Id &&
                token.UsedAt == null)
            .ToListAsync();

        foreach (var token in oldTokens)
        {
            token.UsedAt = DateTime.UtcNow;
        }

        // Generate a cryptographically secure random token.
        var tokenBytes = RandomNumberGenerator.GetBytes(32);

        var rawToken = Convert.ToHexString(tokenBytes);

        // Only save the token hash.
        var tokenHash = HashResetToken(rawToken);

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        };

        _context.PasswordResetTokens.Add(resetToken);

        await _context.SaveChangesAsync();

        var frontendUrl =
            (_configuration["FrontendBaseUrl"]
             ?? "http://localhost:3000")
            .TrimEnd('/');

        var resetLink =
            $"{frontendUrl}/reset-password" +
            $"?token={Uri.EscapeDataString(rawToken)}";

        await SendResetEmail(
            user.Email,
            user.FullName,
            resetLink
        );

        return Ok(genericResponse);
    }

    private static string HashResetToken(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);

        var hashBytes = SHA256.HashData(tokenBytes);

        return Convert.ToHexString(hashBytes);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
    ResetPasswordDto dto)
    {
        var tokenHash = HashResetToken(dto.Token);

        var resetToken = await _context.PasswordResetTokens
            .FirstOrDefaultAsync(token =>
                token.TokenHash == tokenHash);

        if (resetToken == null)
        {
            return BadRequest(
                "The reset link is invalid or has expired."
            );
        }

        if (resetToken.UsedAt != null)
        {
            return BadRequest(
                "The reset link has already been used."
            );
        }

        if (resetToken.ExpiresAt <= DateTime.UtcNow)
        {
            return BadRequest(
                "The reset link is invalid or has expired."
            );
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(user =>
                user.Id == resetToken.UserId &&
                user.Role == "customer");

        if (user == null)
        {
            return BadRequest(
                "The reset link is invalid."
            );
        }

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        resetToken.UsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Password reset successfully."
        });
    }

    private async Task SendResetEmail(
    string recipient,
    string fullName,
    string resetLink)
    {
        var smtpHost = _configuration["Smtp:Host"];

        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            // Local-development fallback
            Console.WriteLine(
                $"Password reset for {recipient}: {resetLink}"
            );

            return;
        }

        var smtpPort =
            int.TryParse(
                _configuration["Smtp:Port"],
                out var configuredPort
            )
                ? configuredPort
                : 587;

        var fromAddress =
            _configuration["Smtp:From"]
            ?? "no-reply@alphaauto.app";

        using var email = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(
                fromAddress,
                "Alpha Auto"
            ),

            Subject = "Reset your Alpha Auto password",

            Body =
                $"Hello {fullName},\n\n" +
                "We received a request to reset your " +
                "Alpha Auto customer password.\n\n" +
                "Use the link below within 30 minutes:\n\n" +
                $"{resetLink}\n\n" +
                "If you did not request this reset, " +
                "you can ignore this email."
        };

        email.To.Add(recipient);

        using var smtpClient =
            new System.Net.Mail.SmtpClient(
                smtpHost,
                smtpPort
            )
            {
                EnableSsl = true,

                Credentials =
                    new System.Net.NetworkCredential(
                        _configuration["Smtp:Username"],
                        _configuration["Smtp:Password"]
                    )
            };

        await smtpClient.SendMailAsync(email);
    }
}