using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class UsersController : ControllerBase
{
    private static readonly string[] AllowedRoles =
    [
        "admin",
        "dispatcher",
        "customer",
        "driver",
        "provider",
        "supplier",
        "mechanic",
        "tow_provider"
    ];

    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: /api/Users
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role)
    {
        var query = _context.Users
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search
                .Trim()
                .ToLower();

            query = query.Where(user =>
                user.FullName.ToLower().Contains(value) ||
                user.Email.ToLower().Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = role
                .Trim()
                .ToLower();

            query = query.Where(user =>
                user.Role == normalizedRole);
        }

        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Phone,
                user.Role,
                user.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // POST: /api/Users
    [HttpPost]
    public async Task<IActionResult> Create(
        AdminCreateUserDto dto)
    {
        var role = dto.Role
            .Trim()
            .ToLower();

        var email = dto.Email
            .Trim()
            .ToLower();

        if (!AllowedRoles.Contains(role))
        {
            return BadRequest("Invalid role.");
        }

        var emailExists = await _context.Users
            .AnyAsync(user =>
                user.Email.ToLower() == email);

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
                Role = role,

                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        dto.Password
                    ),

                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            await CreateRoleProfile(user, dto);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Phone,
                    user.Role,
                    user.CreatedAt
                }
            );
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // PUT: /api/Users/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        AdminUpdateUserDto dto)
    {
        var user = await _context.Users
            .FindAsync(id);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        var role = dto.Role
            .Trim()
            .ToLower();

        if (!AllowedRoles.Contains(role))
        {
            return BadRequest("Invalid role.");
        }

        user.FullName = dto.FullName.Trim();
        user.Phone = dto.Phone?.Trim();
        user.Role = role;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // POST: /api/Users/{id}/reset-password
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        AdminResetPasswordDto dto)
    {
        var user = await _context.Users
            .FindAsync(id);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword
            );

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Password updated successfully."
        });
    }

    // DELETE: /api/Users/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        var currentUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (currentUserId == id.ToString())
        {
            return BadRequest(
                "You cannot deactivate your own account."
            );
        }

        user.IsActive = false;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task CreateRoleProfile(
        User user,
        AdminCreateUserDto dto)
    {
        switch (user.Role)
        {
            case "customer":
                await _context.Database
                    .ExecuteSqlInterpolatedAsync($@"
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
                break;

            case "driver":
                _context.Drivers.Add(new Driver
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = dto.Phone,
                    VehicleType = dto.VehicleType,
                    PlateNumber = dto.PlateNumber,
                    Territory = dto.Territory,
                    AvailabilityStatus = "available",
                    ActiveJobs = 0,
                    ResponseRate = 100,
                    CreatedAt = DateTime.UtcNow
                });
                break;

            case "supplier":
            case "provider":
                _context.Suppliers.Add(new Supplier
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Name = user.FullName,
                    ContactNumber = dto.Phone,
                    Address = dto.Address,
                    Territory = dto.Territory ?? "",
                    AvailabilityStatus = "available",
                    CurrentWorkload = 0,
                    ResponseRate = 100,
                    CreatedAt = DateTime.UtcNow
                });
                break;

            case "mechanic":
                _context.Mechanics.Add(new Mechanic
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = dto.Phone,
                    ServiceArea = dto.Territory,
                    AvailabilityStatus = "available",
                    ActiveJobs = 0,
                    ResponseRate = 100,
                    CreatedAt = DateTime.UtcNow
                });
                break;

                // Admin, dispatcher, and tow_provider
                // currently only need the users table.
        }
    }
}