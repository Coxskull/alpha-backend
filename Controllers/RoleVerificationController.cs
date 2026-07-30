using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/role-verifications")]
[Authorize]
public class RoleVerificationController : ControllerBase
{
    private const long MaximumFileSize = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "application/pdf"
        };

    private static readonly HashSet<string> VerifiableRoles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            EntrepreneurRoles.Driver,
            EntrepreneurRoles.Mechanic,
            EntrepreneurRoles.Supplier
        };

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public RoleVerificationController(
        AppDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var applications = await _context
            .RoleVerificationApplications
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Include(x => x.Documents)
            .OrderBy(x => x.RoleKey)
            .Select(x => new
            {
                x.Id,
                x.RoleKey,
                x.Status,
                x.LegalName,
                x.BusinessName,
                x.LicenseNumber,
                x.VehiclePlateNumber,
                x.YearsOfExperience,
                x.BusinessAddress,
                x.ApplicantNotes,
                x.ReviewerNotes,
                x.RejectionReason,
                x.SubmittedAt,
                x.ReviewedAt,

                documents = x.Documents.Select(document => new
                {
                    document.Id,
                    document.DocumentType,
                    document.OriginalFileName,
                    document.ContentType,
                    document.FileSizeBytes,
                    document.VerificationStatus,
                    document.ReviewerNotes,
                    document.UploadedAt
                })
            })
            .ToListAsync(cancellationToken);

        return Ok(applications);
    }

    [HttpPost]
    public async Task<IActionResult> SaveApplication(
        [FromBody] SubmitVerificationDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var role = NormalizeRole(dto.RoleKey);

        if (!VerifiableRoles.Contains(role))
        {
            return BadRequest(new
            {
                message = "This role does not require operational verification."
            });
        }

        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value &&
                     x.RoleKey == role,
                cancellationToken);

        if (userRole == null)
        {
            return BadRequest(new
            {
                message = "This role is not assigned to your account."
            });
        }

        if (userRole.Status == "active")
        {
            return Conflict(new
            {
                message = "This role is already verified."
            });
        }

        var application = await _context
            .RoleVerificationApplications
            .FirstOrDefaultAsync(
                x => x.UserId == userId.Value &&
                     x.RoleKey == role,
                cancellationToken);

        if (application == null)
        {
            application = new RoleVerificationApplication
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                RoleKey = role,
                Status = "draft",
                CreatedAt = DateTime.UtcNow
            };

            _context.RoleVerificationApplications.Add(application);
        }

        application.LegalName = dto.LegalName?.Trim();
        application.BusinessName = dto.BusinessName?.Trim();
        application.IdentificationNumber =
            dto.IdentificationNumber?.Trim();
        application.LicenseNumber = dto.LicenseNumber?.Trim();
        application.VehiclePlateNumber =
            dto.VehiclePlateNumber?.Trim();
        application.YearsOfExperience = dto.YearsOfExperience;
        application.BusinessAddress =
            dto.BusinessAddress?.Trim();
        application.ApplicantNotes =
            dto.ApplicantNotes?.Trim();
        application.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            application.Id,
            application.RoleKey,
            application.Status
        });
    }

    [HttpPost("{applicationId:guid}/documents")]
    [RequestSizeLimit(MaximumFileSize)]
    public async Task<IActionResult> UploadDocument(
        Guid applicationId,
        [FromForm] UploadVerificationDocumentDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var application = await _context
            .RoleVerificationApplications
            .FirstOrDefaultAsync(
                x => x.Id == applicationId &&
                     x.UserId == userId.Value,
                cancellationToken);

        if (application == null)
        {
            return NotFound(new
            {
                message = "Verification application was not found."
            });
        }

        if (application.Status is "approved" or "under_review")
        {
            return Conflict(new
            {
                message = "Documents cannot be changed at this stage."
            });
        }

        if (dto.File == null || dto.File.Length == 0)
        {
            return BadRequest(new
            {
                message = "Select a document to upload."
            });
        }

        if (dto.File.Length > MaximumFileSize)
        {
            return BadRequest(new
            {
                message = "The maximum file size is 10 MB."
            });
        }

        if (!AllowedContentTypes.Contains(dto.File.ContentType))
        {
            return BadRequest(new
            {
                message = "Only JPG, PNG, WEBP, and PDF files are allowed."
            });
        }

        var extension = dto.File.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => throw new InvalidOperationException(
                "Unsupported file type.")
        };

        var root = Path.Combine(
            _environment.ContentRootPath,
            "private-uploads",
            "verification",
            userId.Value.ToString(),
            application.RoleKey);

        Directory.CreateDirectory(root);

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(root, storedName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await dto.File.CopyToAsync(
                stream,
                cancellationToken);
        }

        var document = new RoleVerificationDocument
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            DocumentType = dto.DocumentType.Trim().ToLowerInvariant(),
            OriginalFileName =
                Path.GetFileName(dto.File.FileName),
            StoragePath = fullPath,
            ContentType = dto.File.ContentType,
            FileSizeBytes = dto.File.Length,
            VerificationStatus = "pending",
            UploadedAt = DateTime.UtcNow
        };

        _context.RoleVerificationDocuments.Add(document);

        application.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            document.Id,
            document.DocumentType,
            document.OriginalFileName
        });
    }

    [HttpPost("{applicationId:guid}/submit")]
    public async Task<IActionResult> Submit(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized();
        }

        var application = await _context
            .RoleVerificationApplications
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(
                x => x.Id == applicationId &&
                     x.UserId == userId.Value,
                cancellationToken);

        if (application == null)
        {
            return NotFound();
        }

        var missingDocuments =
            GetRequiredDocumentTypes(application.RoleKey)
                .Where(required =>
                    !application.Documents.Any(document =>
                        document.DocumentType.Equals(
                            required,
                            StringComparison.OrdinalIgnoreCase)))
                .ToList();

        if (missingDocuments.Count > 0)
        {
            return BadRequest(new
            {
                message = "Required documents are missing.",
                missingDocuments
            });
        }

        application.Status = "under_review";
        application.SubmittedAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;
        application.RejectionReason = null;

        var userRole = await _context.UserRoles
            .FirstAsync(
                x => x.UserId == userId.Value &&
                     x.RoleKey == application.RoleKey,
                cancellationToken);

        userRole.Status = "under_review";
        userRole.ActivatedAt = null;

        await UpdateOperationalAvailability(
            userId.Value,
            application.RoleKey,
            "under_review",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Verification submitted for administrator review.",
            status = application.Status
        });
    }

    private async Task UpdateOperationalAvailability(
        Guid userId,
        string role,
        string status,
        CancellationToken cancellationToken)
    {
        if (role == EntrepreneurRoles.Driver)
        {
            var driver = await _context.Drivers
                .FirstOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

            if (driver != null)
            {
                driver.AvailabilityStatus = status;
            }
        }

        if (role == EntrepreneurRoles.Mechanic)
        {
            var mechanic = await _context.Mechanics
                .FirstOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

            if (mechanic != null)
            {
                mechanic.AvailabilityStatus = status;
            }
        }

        if (role == EntrepreneurRoles.Supplier)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

            if (supplier != null)
            {
                supplier.AvailabilityStatus = status;
            }
        }
    }

    private Guid? GetUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(ClaimTypes.Name) ??
            User.FindFirstValue("sub");

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    private static string NormalizeRole(string role)
    {
        return role.Trim()
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");
    }

    private static IReadOnlyCollection<string>
        GetRequiredDocumentTypes(string role)
    {
        return role switch
        {
            EntrepreneurRoles.Driver => new[]
            {
                "government_id",
                "drivers_license",
                "vehicle_registration",
                "vehicle_photo",
                "selfie_with_id"
            },

            EntrepreneurRoles.Mechanic => new[]
            {
                "government_id",
                "workplace_photo",
                "professional_proof",
                "selfie_with_id"
            },

            EntrepreneurRoles.Supplier => new[]
            {
                "government_id",
                "business_registration",
                "storefront_photo",
                "business_address_proof"
            },

            _ => Array.Empty<string>()
        };
    }

    [HttpGet("admin/pending")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetPending(
    CancellationToken cancellationToken)
    {
        var applications = await _context
            .RoleVerificationApplications
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Documents)
            .Where(x =>
                x.Status == "under_review" ||
                x.Status == "needs_more_information")
            .OrderBy(x => x.SubmittedAt)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.RoleKey,
                x.Status,
                x.LegalName,
                x.BusinessName,
                x.LicenseNumber,
                x.VehiclePlateNumber,
                x.YearsOfExperience,
                x.BusinessAddress,
                x.ApplicantNotes,
                x.SubmittedAt,

                applicant = new
                {
                    x.User.FullName,
                    x.User.Email,
                    x.User.Phone
                },

                documents = x.Documents.Select(document => new
                {
                    document.Id,
                    document.DocumentType,
                    document.OriginalFileName,
                    document.ContentType,
                    document.VerificationStatus
                })
            })
            .ToListAsync(cancellationToken);

        return Ok(applications);
    }

    [HttpPost("admin/{applicationId:guid}/review")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Review(
        Guid applicationId,
        [FromBody] ReviewVerificationDto dto,
        CancellationToken cancellationToken)
    {
        var reviewerId = GetUserId();

        if (reviewerId == null)
        {
            return Unauthorized();
        }

        var decision = dto.Decision
            .Trim()
            .ToLowerInvariant();

        if (decision is not
            ("approved" or
             "rejected" or
             "needs_more_information"))
        {
            return BadRequest(new
            {
                message = "Invalid review decision."
            });
        }

        var application = await _context
            .RoleVerificationApplications
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(
                x => x.Id == applicationId,
                cancellationToken);

        if (application == null)
        {
            return NotFound();
        }

        if (application.Status != "under_review")
        {
            return Conflict(new
            {
                message = "This application is not awaiting review."
            });
        }

        if (decision == "rejected" &&
            string.IsNullOrWhiteSpace(dto.RejectionReason))
        {
            return BadRequest(new
            {
                message = "A rejection reason is required."
            });
        }

        var userRole = await _context.UserRoles
            .FirstAsync(
                x => x.UserId == application.UserId &&
                     x.RoleKey == application.RoleKey,
                cancellationToken);

        application.Status = decision;
        application.ReviewerNotes =
            dto.ReviewerNotes?.Trim();
        application.RejectionReason =
            decision == "rejected"
                ? dto.RejectionReason?.Trim()
                : null;
        application.ReviewedByUserId = reviewerId;
        application.ReviewedAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;

        if (decision == "approved")
        {
            userRole.Status = "active";
            userRole.ActivatedAt = DateTime.UtcNow;

            foreach (var document in application.Documents)
            {
                document.VerificationStatus = "accepted";
                document.ReviewedAt = DateTime.UtcNow;
            }

            await UpdateOperationalAvailability(
                application.UserId,
                application.RoleKey,
                "available",
                cancellationToken);
        }
        else if (decision == "rejected")
        {
            userRole.Status = "rejected";
            userRole.ActivatedAt = null;

            await UpdateOperationalAvailability(
                application.UserId,
                application.RoleKey,
                "verification_rejected",
                cancellationToken);
        }
        else
        {
            userRole.Status = "profile_incomplete";
            userRole.ActivatedAt = null;

            await UpdateOperationalAvailability(
                application.UserId,
                application.RoleKey,
                "needs_more_information",
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = $"Application {decision}.",
            application.Status,
            roleStatus = userRole.Status
        });
    }
}