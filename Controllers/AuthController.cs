using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Services;
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
using Alpha.API.Security;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ReferralCodeService _referralCodeService;

    public AuthController(
    AppDbContext context,
    IConfiguration configuration,
    ReferralCodeService referralCodeService)
    {
        _context = context;
        _configuration = configuration;
        _referralCodeService = referralCodeService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
     [FromBody] RegisterDto dto,
     CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // ---------------------------------------------------------
        // 1. Normalize basic registration information
        // ---------------------------------------------------------

        var fullName = dto.FullName?.Trim() ?? string.Empty;
        var email = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var phone = string.IsNullOrWhiteSpace(dto.Phone)
            ? null
            : dto.Phone.Trim();

        var city = dto.City?.Trim() ?? string.Empty;

        var state = string.IsNullOrWhiteSpace(dto.State)
            ? null
            : dto.State.Trim();

        var country = string.IsNullOrWhiteSpace(dto.Country)
            ? "MX"
            : dto.Country.Trim().ToUpperInvariant();

        var preferredLanguage =
            string.IsNullOrWhiteSpace(dto.PreferredLanguage)
                ? "es"
                : dto.PreferredLanguage.Trim().ToLowerInvariant();

        var businessName =
            string.IsNullOrWhiteSpace(dto.BusinessName)
                ? null
                : dto.BusinessName.Trim();

        var entrepreneurialGoal =
            string.IsNullOrWhiteSpace(dto.EntrepreneurialGoal)
                ? null
                : dto.EntrepreneurialGoal.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest(new
            {
                message = "Full name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new
            {
                message = "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return BadRequest(new
            {
                message =
                    "Enter the city where you will build your Alpha network."
            });
        }

        if (country.Length != 2)
        {
            return BadRequest(new
            {
                message =
                    "Country must use a valid two-letter country code, such as MX."
            });
        }

        // ---------------------------------------------------------
        // 2. Normalize and validate selected roles
        // ---------------------------------------------------------

        var selectedRoles = (dto.SelectedRoles ?? new List<string>())
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(NormalizeEntrepreneurRole)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedRoles.Count == 0)
        {
            return BadRequest(new
            {
                message = "Choose at least one Alpha entrepreneur role."
            });
        }

        var invalidRoles = selectedRoles
            .Where(role =>
                !EntrepreneurRoles.PublicRegistrationRoles.Contains(role))
            .ToList();

        if (invalidRoles.Count > 0)
        {
            return BadRequest(new
            {
                message = "One or more selected roles are invalid.",
                invalidRoles
            });
        }

        if (!dto.AcceptTerms)
        {
            return BadRequest(new
            {
                message =
                    "You must accept the Alpha terms and conditions."
            });
        }

        if (!dto.AcceptRewardsPolicy)
        {
            return BadRequest(new
            {
                message =
                    "You must accept the transaction-based rewards policy."
            });
        }

        var primaryRole = ResolvePrimaryRole(
            selectedRoles,
            dto.PrimaryRole
        );

        // ---------------------------------------------------------
        // 3. Validate email uniqueness
        // ---------------------------------------------------------

        var emailExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Email.ToLower() == email,
                cancellationToken
            );

        if (emailExists)
        {
            return Conflict(new
            {
                message = "Email already exists."
            });
        }

        // ---------------------------------------------------------
        // 4. Validate optional referral code
        // ---------------------------------------------------------

        User? referrer = null;

        if (!string.IsNullOrWhiteSpace(dto.ReferralCode))
        {
            var normalizedReferralCode =
                dto.ReferralCode.Trim().ToUpperInvariant();

            referrer = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    user =>
                        user.ReferralCode != null &&
                        user.ReferralCode.ToUpper() ==
                            normalizedReferralCode &&
                        user.IsActive,
                    cancellationToken
                );

            if (referrer == null)
            {
                return BadRequest(new
                {
                    message =
                        "The referral code is invalid or inactive."
                });
            }

            if (referrer.Email.Equals(
                email,
                StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message =
                        "You cannot use your own referral code."
                });
            }
        }

        // ---------------------------------------------------------
        // 5. Validate role-specific information
        // ---------------------------------------------------------

        if (selectedRoles.Contains(
                EntrepreneurRoles.Supplier,
                StringComparer.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(businessName))
        {
            return BadRequest(new
            {
                message =
                    "A business or store name is required when registering as an Auto Parts Store."
            });
        }

        // ---------------------------------------------------------
        // 6. Begin registration transaction
        // ---------------------------------------------------------

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken
            );

        try
        {
            var now = DateTime.UtcNow;

            var generatedReferralCode =
                await _referralCodeService.GenerateUniqueCodeAsync(
                    fullName,
                    cancellationToken
                );

            // -----------------------------------------------------
            // 7. Create the main user
            // -----------------------------------------------------

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                Phone = phone,

                // Keep one primary role for backward compatibility.
                Role = primaryRole,

                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(dto.Password),

                CreatedAt = now,
                IsActive = true,

                ReferralCode = generatedReferralCode,
                ReferredByUserId = referrer?.Id,
                ReferralJoinedAt = referrer == null
                    ? null
                    : now
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync(cancellationToken);

            // -----------------------------------------------------
            // 8. Create entrepreneur profile
            // -----------------------------------------------------

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
            INSERT INTO entrepreneur_profiles (
                id,
                user_id,
                city,
                state,
                country,
                preferred_language,
                business_name,
                entrepreneurial_goal,
                onboarding_status,
                terms_accepted_at,
                rewards_policy_accepted_at,
                created_at,
                updated_at
            )
            VALUES (
                {Guid.NewGuid()},
                {user.Id},
                {city},
                {state},
                {country},
                {preferredLanguage},
                {businessName},
                {entrepreneurialGoal},
                {"roles_selected"},
                {now},
                {now},
                {now},
                {now}
            )
            ON CONFLICT (user_id) DO UPDATE SET
                city = EXCLUDED.city,
                state = EXCLUDED.state,
                country = EXCLUDED.country,
                preferred_language = EXCLUDED.preferred_language,
                business_name = EXCLUDED.business_name,
                entrepreneurial_goal =
                    EXCLUDED.entrepreneurial_goal,
                onboarding_status =
                    EXCLUDED.onboarding_status,
                terms_accepted_at =
                    EXCLUDED.terms_accepted_at,
                rewards_policy_accepted_at =
                    EXCLUDED.rewards_policy_accepted_at,
                updated_at = EXCLUDED.updated_at;
            """,
                cancellationToken
            );

            // -----------------------------------------------------
            // 9. Create every selected role
            // -----------------------------------------------------

            foreach (var role in selectedRoles)
            {
                var roleStatus = GetInitialRoleStatus(role);

                var isPrimary = role.Equals(
                    primaryRole,
                    StringComparison.OrdinalIgnoreCase
                );

                DateTime? activatedAt =
                    roleStatus == "active"
                        ? now
                        : null;

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                INSERT INTO user_roles (
                    id,
                    user_id,
                    role_key,
                    status,
                    is_primary,
                    activated_at,
                    created_at
                )
                VALUES (
                    {Guid.NewGuid()},
                    {user.Id},
                    {role},
                    {roleStatus},
                    {isPrimary},
                    {activatedAt},
                    {now}
                )
                ON CONFLICT (user_id, role_key)
                DO UPDATE SET
                    status = EXCLUDED.status,
                    is_primary = EXCLUDED.is_primary,
                    activated_at = EXCLUDED.activated_at;
                """,
                    cancellationToken
                );
            }

            // -----------------------------------------------------
            // 10. Create Vehicle Owner / Customer profile
            // -----------------------------------------------------

            if (selectedRoles.Contains(
                EntrepreneurRoles.Customer,
                StringComparer.OrdinalIgnoreCase))
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
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
                ON CONFLICT (id) DO UPDATE SET
                    full_name = EXCLUDED.full_name,
                    email = EXCLUDED.email,
                    phone = EXCLUDED.phone;
                """,
                    cancellationToken
                );
            }

            // -----------------------------------------------------
            // 11. Create Motorcycle Rider profile
            // -----------------------------------------------------

            if (selectedRoles.Contains(
                EntrepreneurRoles.Driver,
                StringComparer.OrdinalIgnoreCase))
            {
                var existingDriver = await _context.Drivers
                    .AnyAsync(
                        driver => driver.UserId == user.Id,
                        cancellationToken
                    );

                if (!existingDriver)
                {
                    var driver = new Driver
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        FullName = user.FullName,
                        PhoneNumber = user.Phone,
                        Email = user.Email,

                        // The rider must complete the operational profile
                        // before receiving delivery jobs.
                        AvailabilityStatus = "profile_incomplete",

                        Territory = city,
                        VehicleType = null,
                        PlateNumber = null,
                        ActiveJobs = 0,
                        ResponseRate = 100,
                        CreatedAt = now,
                        LastSeenAt = null
                    };

                    _context.Drivers.Add(driver);
                }
            }

            // -----------------------------------------------------
            // 12. Create Mechanic profile
            // -----------------------------------------------------

            if (selectedRoles.Contains(
                EntrepreneurRoles.Mechanic,
                StringComparer.OrdinalIgnoreCase))
            {
                var existingMechanic = await _context.Mechanics
                    .AnyAsync(
                        mechanic => mechanic.UserId == user.Id,
                        cancellationToken
                    );

                if (!existingMechanic)
                {
                    var mechanic = new Mechanic
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Phone = user.Phone,
                        ServiceArea = city,

                        // Mechanic onboarding must be completed first.
                        AvailabilityStatus = "profile_incomplete",

                        Latitude = null,
                        Longitude = null,
                        ServiceRadiusKm = 10,
                        ActiveJobs = 0,
                        ResponseRate = 100,
                        CreatedAt = now
                    };

                    _context.Mechanics.Add(mechanic);
                }
            }

            // -----------------------------------------------------
            // 13. Create Auto Parts Store / Supplier profile
            // -----------------------------------------------------

            if (selectedRoles.Contains(
                EntrepreneurRoles.Supplier,
                StringComparer.OrdinalIgnoreCase))
            {
                var existingSupplier = await _context.Suppliers
                    .AnyAsync(
                        supplier => supplier.UserId == user.Id,
                        cancellationToken
                    );

                if (!existingSupplier)
                {
                    var supplier = new Supplier
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Name = businessName ?? user.FullName,
                        ContactNumber = user.Phone,
                        Address = null,

                        // Store profile and inventory must be completed first.
                        AvailabilityStatus = "profile_incomplete",

                        Territory = city,
                        CurrentWorkload = 0,
                        ResponseRate = 100,
                        CreatedAt = now
                    };

                    _context.Suppliers.Add(supplier);
                }
            }

            // -----------------------------------------------------
            // 14. Create Community Builder profile
            // -----------------------------------------------------

            if (selectedRoles.Contains(
                EntrepreneurRoles.CommunityBuilder,
                StringComparer.OrdinalIgnoreCase))
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                INSERT INTO community_builder_profiles (
                    id,
                    user_id,
                    home_city,
                    home_state,
                    country,
                    builder_status,
                    total_cities_connected,
                    approved_at,
                    created_at,
                    updated_at
                )
                VALUES (
                    {Guid.NewGuid()},
                    {user.Id},
                    {city},
                    {state},
                    {country},
                    {"active"},
                    {1},
                    {now},
                    {now},
                    {now}
                )
                ON CONFLICT (user_id) DO UPDATE SET
                    home_city = EXCLUDED.home_city,
                    home_state = EXCLUDED.home_state,
                    country = EXCLUDED.country,
                    builder_status = EXCLUDED.builder_status,
                    updated_at = EXCLUDED.updated_at;
                """,
                    cancellationToken
                );

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                INSERT INTO community_builder_cities (
                    id,
                    builder_user_id,
                    city,
                    state,
                    country,
                    member_count,
                    active_member_count,
                    completed_transaction_count,
                    connected_at
                )
                VALUES (
                    {Guid.NewGuid()},
                    {user.Id},
                    {city},
                    {state},
                    {country},
                    {0},
                    {0},
                    {0},
                    {now}
                )
                ON CONFLICT (
                    builder_user_id,
                    city,
                    state,
                    country
                )
                DO NOTHING;
                """,
                    cancellationToken
                );
            }

            // -----------------------------------------------------
            // 15. Create empty business activity record
            // -----------------------------------------------------

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
            INSERT INTO member_business_activity (
                id,
                user_id,
                completed_orders,
                completed_service_requests,
                completed_deliveries,
                fulfilled_parts_orders,
                customer_purchases,
                gross_transaction_value,
                first_business_activity_at,
                last_business_activity_at,
                is_business_active,
                updated_at
            )
            VALUES (
                {Guid.NewGuid()},
                {user.Id},
                {0},
                {0},
                {0},
                {0},
                {0},
                {0m},
                {null},
                {null},
                {false},
                {now}
            )
            ON CONFLICT (user_id) DO NOTHING;
            """,
                cancellationToken
            );

            // Save Driver, Mechanic, and Supplier entities.
            await _context.SaveChangesAsync(cancellationToken);

            // -----------------------------------------------------
            // 16. Commit registration
            // -----------------------------------------------------

            await transaction.CommitAsync(cancellationToken);

            // -----------------------------------------------------
            // 17. Return registration result
            // -----------------------------------------------------

            return Ok(new
            {
                message =
                    "Welcome to the Alpha Entrepreneur Network.",

                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Phone,

                    role = user.Role,
                    primaryRole = user.Role,
                    roles = selectedRoles,

                    city,
                    state,
                    country,
                    businessName,

                    user.ReferralCode,
                    user.ReferredByUserId,
                    user.ReferralJoinedAt,

                    isCommunityBuilder =
                        selectedRoles.Contains(
                            EntrepreneurRoles.CommunityBuilder,
                            StringComparer.OrdinalIgnoreCase
                        ),

                    onboardingRequired = selectedRoles
                        .Where(role =>
                            GetInitialRoleStatus(role) != "active")
                        .ToList()
                },

                referredBy = referrer == null
                    ? null
                    : new
                    {
                        referrer.Id,
                        referrer.FullName,
                        referrer.Role,
                        referrer.ReferralCode
                    },

                nextStep = selectedRoles.Count > 1
                    ? "/select-workspace"
                    : GetDashboardRoute(primaryRole)
            });
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "Registration could not be completed because the account data could not be saved.",
                    detail = exception.InnerException?.Message ??
                             exception.Message
                }
            );
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "An unexpected error occurred during registration.",
                    detail = exception.Message
                }
            );
        }
    }

    private static string NormalizeEntrepreneurRole(string role)
    {
        var normalized = role
            .Trim()
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");

        return normalized switch
        {
            // Motorcycle Rider aliases
            "rider" => EntrepreneurRoles.Driver,
            "motorcycle_rider" => EntrepreneurRoles.Driver,
            "delivery_rider" => EntrepreneurRoles.Driver,

            // Mechanic aliases
            "automotive_mechanic" => EntrepreneurRoles.Mechanic,

            // Auto Parts Store aliases
            "provider" => EntrepreneurRoles.Supplier,
            "store" => EntrepreneurRoles.Supplier,
            "auto_parts_store" => EntrepreneurRoles.Supplier,
            "auto_part_store" => EntrepreneurRoles.Supplier,
            "parts_store" => EntrepreneurRoles.Supplier,

            // Vehicle Owner aliases
            "vehicle_owner" => EntrepreneurRoles.Customer,
            "car_owner" => EntrepreneurRoles.Customer,

            // Community Builder aliases
            "communitybuilder" =>
                EntrepreneurRoles.CommunityBuilder,

            "builder" =>
                EntrepreneurRoles.CommunityBuilder,

            _ => normalized
        };
    }

    private static string ResolvePrimaryRole(
    IReadOnlyCollection<string> selectedRoles,
    string? requestedPrimaryRole)
    {
        var normalizedRequestedRole =
            string.IsNullOrWhiteSpace(requestedPrimaryRole)
                ? null
                : NormalizeEntrepreneurRole(requestedPrimaryRole);

        if (normalizedRequestedRole != null &&
            selectedRoles.Contains(
                normalizedRequestedRole,
                StringComparer.OrdinalIgnoreCase))
        {
            return normalizedRequestedRole;
        }

        // Community Builder becomes the preferred dashboard
        // when no valid primary role was explicitly selected.
        if (selectedRoles.Contains(
            EntrepreneurRoles.CommunityBuilder,
            StringComparer.OrdinalIgnoreCase))
        {
            return EntrepreneurRoles.CommunityBuilder;
        }

        if (selectedRoles.Contains(
            EntrepreneurRoles.Supplier,
            StringComparer.OrdinalIgnoreCase))
        {
            return EntrepreneurRoles.Supplier;
        }

        if (selectedRoles.Contains(
            EntrepreneurRoles.Mechanic,
            StringComparer.OrdinalIgnoreCase))
        {
            return EntrepreneurRoles.Mechanic;
        }

        if (selectedRoles.Contains(
            EntrepreneurRoles.Driver,
            StringComparer.OrdinalIgnoreCase))
        {
            return EntrepreneurRoles.Driver;
        }

        return EntrepreneurRoles.Customer;
    }

    private static string GetInitialRoleStatus(string role)
    {
        return role switch
        {
            // These roles can be used immediately.
            EntrepreneurRoles.Customer => "active",
            EntrepreneurRoles.CommunityBuilder => "active",

            // These require additional operational information
            // and potentially admin verification.
            EntrepreneurRoles.Driver => "profile_incomplete",
            EntrepreneurRoles.Mechanic => "profile_incomplete",
            EntrepreneurRoles.Supplier => "profile_incomplete",

            _ => "pending"
        };
    }

    private static string GetDashboardRoute(string primaryRole)
    {
        return primaryRole switch
        {
            EntrepreneurRoles.CommunityBuilder =>
                "/entrepreneur/dashboard",

            EntrepreneurRoles.Driver =>
                "/driver/dashboard",

            EntrepreneurRoles.Mechanic =>
                "/mechanic/dashboard",

            EntrepreneurRoles.Supplier =>
                "/provider/dashboard",

            EntrepreneurRoles.Customer =>
                "/customer",

            _ => "/"
        };
    }


[AllowAnonymous]
[HttpPost("login")]
public async Task<IActionResult> Login(
    [FromBody] LoginDto dto,
    CancellationToken cancellationToken)
    {
        if (dto is null ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new
            {
                message = "Email and password are required."
            });
        }

        var normalizedEmail =
            dto.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                existingUser =>
                    existingUser.Email.ToLower() ==
                        normalizedEmail &&
                    existingUser.IsActive,
                cancellationToken);

        if (user is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var validPassword =
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

        if (!validPassword)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var selectedRoles = await _context.UserRoles
            .AsNoTracking()
            .Where(role =>
                role.UserId == user.Id &&
                role.Status != "rejected" &&
                role.Status != "suspended")
            .Select(role => role.RoleKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Backward compatibility for accounts created
        // before the user_roles table was introduced.
        if (selectedRoles.Count == 0 &&
            !string.IsNullOrWhiteSpace(user.Role))
        {
            selectedRoles.Add(user.Role);
        }

        if (!selectedRoles.Contains(
            user.Role,
            StringComparer.OrdinalIgnoreCase))
        {
            selectedRoles.Insert(0, user.Role);
        }

        var token = GenerateToken(
            user,
            selectedRoles);

        Guid? supplierId = null;
        Guid? driverId = null;
        Guid? mechanicId = null;

        if (selectedRoles.Contains(
            EntrepreneurRoles.Supplier,
            StringComparer.OrdinalIgnoreCase))
        {
            supplierId = await _context.Suppliers
                .AsNoTracking()
                .Where(supplier =>
                    supplier.UserId == user.Id)
                .Select(supplier =>
                    (Guid?)supplier.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);
        }

        if (selectedRoles.Contains(
            EntrepreneurRoles.Driver,
            StringComparer.OrdinalIgnoreCase))
        {
            driverId = await _context.Drivers
                .AsNoTracking()
                .Where(driver =>
                    driver.UserId == user.Id)
                .Select(driver =>
                    (Guid?)driver.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);
        }

        if (selectedRoles.Contains(
            EntrepreneurRoles.Mechanic,
            StringComparer.OrdinalIgnoreCase))
        {
            mechanicId = await _context.Mechanics
                .AsNoTracking()
                .Where(mechanic =>
                    mechanic.UserId == user.Id)
                .Select(mechanic =>
                    (Guid?)mechanic.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);
        }

        return Ok(new
        {
            token,

            user = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Phone,

                role = user.Role,
                primaryRole = user.Role,
                roles = selectedRoles,

                user.ReferralCode,
                user.ReferredByUserId,

                supplierId,
                driverId,
                mechanicId,

                nextStep = selectedRoles.Count > 1
                    ? "/select-workspace"
                    : GetDashboardRoute(user.Role)
            }
        });
    }

    private string GenerateToken(
        User user,
        IReadOnlyCollection<string> selectedRoles)
    {
        var jwtKey =
            _configuration["Jwt:Key"] ??
            throw new InvalidOperationException(
                "JWT key is missing.");

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
    {
        new(
            JwtRegisteredClaimNames.Sub,
            user.Id.ToString()),

        new(
            ClaimTypes.NameIdentifier,
            user.Id.ToString()),

        new(
            ClaimTypes.Name,
            user.FullName),

        new(
            ClaimTypes.Email,
            user.Email),

        new(
            ClaimTypes.Role,
            user.Role),

        new(
            "primary_role",
            user.Role)
    };

        foreach (var role in selectedRoles
            .Where(role =>
                !string.IsNullOrWhiteSpace(role))
            .Distinct(
                StringComparer.OrdinalIgnoreCase))
        {
            var alreadyExists =
                claims.Any(claim =>
                    claim.Type ==
                        ClaimTypes.Role &&
                    claim.Value.Equals(
                        role,
                        StringComparison.OrdinalIgnoreCase));

            if (!alreadyExists)
            {
                claims.Add(
                    new Claim(
                        ClaimTypes.Role,
                        role));
            }
        }

        var token =
            new JwtSecurityToken(
                issuer:
                    _configuration["Jwt:Issuer"],

                audience:
                    _configuration["Jwt:Audience"],

                claims:
                    claims,

                expires:
                    DateTime.UtcNow.AddDays(7),

                signingCredentials:
                    credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
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

        var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new(ClaimTypes.Name, user.FullName),
    new(ClaimTypes.Email, user.Email),

    // Primary role for old routes.
    new(ClaimTypes.Role, user.Role),
    new("primary_role", user.Role)
};

        foreach (var role in selectedRoles.Distinct())
        {
            if (!claims.Any(claim =>
                claim.Type == ClaimTypes.Role &&
                claim.Value == role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

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