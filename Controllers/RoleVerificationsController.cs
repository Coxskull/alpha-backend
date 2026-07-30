using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/role-verifications")]
[Authorize]
public sealed class RoleVerificationsController : ControllerBase
{
    private static readonly HashSet<string>
        VerificationRoles =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "driver",
                "mechanic",
                "supplier"
            };

    private readonly AppDbContext _context;
    private readonly VerificationStorageService _storage;
    private readonly ILogger<RoleVerificationsController> _logger;

    public RoleVerificationsController(
        AppDbContext context,
        VerificationStorageService storage,
        ILogger<RoleVerificationsController> logger)
    {
        _context = context;
        _storage = storage;
        _logger = logger;
    }

    // GET /api/role-verifications/mine
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();

        var applications =
            await _context.RoleVerificationApplications
                .AsNoTracking()
                .Where(application =>
                    application.UserId == userId)
                .Include(application =>
                    application.Documents)
                .OrderBy(application =>
                    application.RoleKey)
                .Select(application => new
                {
                    application.Id,
                    application.UserId,
                    application.RoleKey,
                    application.Status,

                    application.LegalName,
                    application.BusinessName,
                    application.IdentificationNumber,
                    application.LicenseNumber,
                    application.VehiclePlateNumber,
                    application.YearsOfExperience,
                    application.BusinessAddress,
                    application.ApplicantNotes,

                    application.ReviewerNotes,
                    application.RejectionReason,
                    application.SubmittedAt,
                    application.ReviewedAt,
                    application.CreatedAt,
                    application.UpdatedAt,

                    documents =
                        application.Documents
                            .OrderBy(document =>
                                document.UploadedAt)
                            .Select(document => new
                            {
                                document.Id,
                                document.DocumentType,
                                document.OriginalFileName,
                                document.ContentType,
                                document.FileSizeBytes,
                                document.VerificationStatus,
                                document.ReviewerNotes,
                                document.UploadedAt,
                                document.ReviewedAt
                            })
                })
                .ToListAsync(cancellationToken);

        return Ok(applications);
    }

    // POST /api/role-verifications
    [HttpPost]
    public async Task<IActionResult> SaveApplication(
        [FromBody]
        SaveRoleVerificationApplicationDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();

        var roleKey =
            NormalizeRole(dto.RoleKey);

        if (!VerificationRoles.Contains(roleKey))
        {
            return BadRequest(new
            {
                message =
                    "Only driver, mechanic, and supplier roles require verification."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.LegalName))
        {
            return BadRequest(new
            {
                message =
                    "Complete legal name is required."
            });
        }

        if (roleKey == "supplier" &&
            string.IsNullOrWhiteSpace(
                dto.BusinessName))
        {
            return BadRequest(new
            {
                message =
                    "Business name is required for supplier verification."
            });
        }

        var userRoleExists =
            await _context.UserRoles
                .AnyAsync(
                    userRole =>
                        userRole.UserId == userId &&
                        userRole.RoleKey == roleKey,
                    cancellationToken);

        if (!userRoleExists)
        {
            return Forbid();
        }

        var application =
            await _context.RoleVerificationApplications
                .FirstOrDefaultAsync(
                    item =>
                        item.UserId == userId &&
                        item.RoleKey == roleKey,
                    cancellationToken);

        var now = DateTime.UtcNow;

        if (application == null)
        {
            application =
                new RoleVerificationApplication
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RoleKey = roleKey,
                    Status = "draft",
                    CreatedAt = now,
                    UpdatedAt = now
                };

            _context.RoleVerificationApplications
                .Add(application);
        }
        else if (
            application.Status == "under_review" ||
            application.Status == "active" ||
            application.Status == "approved")
        {
            return BadRequest(new
            {
                message =
                    "This verification application cannot currently be edited."
            });
        }

        application.LegalName =
            dto.LegalName.Trim();

        application.BusinessName =
            CleanOptional(dto.BusinessName);

        application.IdentificationNumber =
            CleanOptional(
                dto.IdentificationNumber);

        application.LicenseNumber =
            CleanOptional(dto.LicenseNumber);

        application.VehiclePlateNumber =
            CleanOptional(
                dto.VehiclePlateNumber);

        application.YearsOfExperience =
            dto.YearsOfExperience;

        application.BusinessAddress =
            CleanOptional(
                dto.BusinessAddress);

        application.ApplicantNotes =
            CleanOptional(
                dto.ApplicantNotes);

        application.UpdatedAt = now;

        if (application.Status is
            "rejected" or
            "needs_more_information" or
            "profile_incomplete")
        {
            application.Status = "draft";
            application.RejectionReason = null;
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(new
        {
            application.Id,
            application.RoleKey,
            application.Status,
            message =
                "Verification information saved."
        });
    }

    // POST /api/role-verifications/{id}/documents
    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(
        VerificationStorageService.MaximumFileSizeBytes)]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        [FromForm] string documentType,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();

        var application =
            await _context.RoleVerificationApplications
                .Include(item =>
                    item.Documents)
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == id &&
                        item.UserId == userId,
                    cancellationToken);

        if (application == null)
        {
            return NotFound(new
            {
                message =
                    "Verification application was not found."
            });
        }

        if (application.Status is
            "under_review" or
            "active" or
            "approved")
        {
            return BadRequest(new
            {
                message =
                    "Documents cannot be changed while this application is under review or already approved."
            });
        }

        var normalizedDocumentType =
            NormalizeDocumentType(
                documentType);

        if (!IsRequiredDocument(
                application.RoleKey,
                normalizedDocumentType))
        {
            return BadRequest(new
            {
                message =
                    "This document type is not valid for the selected role."
            });
        }

        try
        {
            _storage.ValidateFile(file);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new
            {
                message =
                    exception.Message
            });
        }

        var existingDocument =
            application.Documents
                .FirstOrDefault(
                    document =>
                        document.DocumentType ==
                        normalizedDocumentType);

        string? previousStoragePath =
            existingDocument?.StoragePath;

        string newStoragePath;

        try
        {
            newStoragePath =
                await _storage.UploadAsync(
                    userId,
                    application.Id,
                    application.RoleKey,
                    normalizedDocumentType,
                    file,
                    cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to upload verification document for application {ApplicationId}.",
                application.Id);

            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                new
                {
                    message =
                        "Unable to upload the document to secure storage."
                });
        }

        var now = DateTime.UtcNow;

        if (existingDocument == null)
        {
            existingDocument =
                new RoleVerificationDocument
                {
                    Id = Guid.NewGuid(),
                    ApplicationId =
                        application.Id,
                    DocumentType =
                        normalizedDocumentType,
                    UploadedAt = now
                };

            _context.RoleVerificationDocuments
                .Add(existingDocument);
        }

        existingDocument.OriginalFileName =
            Path.GetFileName(
                file.FileName);

        existingDocument.StoragePath =
            newStoragePath;

        /*
         * New files are no longer stored on Railway.
         */
        existingDocument.FilePath = null;

        existingDocument.ContentType =
            file.ContentType;

        existingDocument.FileSizeBytes =
            file.Length;

        existingDocument.VerificationStatus =
            "pending";

        existingDocument.ReviewerNotes = null;
        existingDocument.ReviewedAt = null;
        existingDocument.ReviewedByUserId = null;
        existingDocument.UploadedAt = now;

        application.Status = "draft";
        application.UpdatedAt = now;

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch
        {
            /*
             * Avoid leaving a newly uploaded object orphaned
             * when the database update fails.
             */
            try
            {
                await _storage.DeleteAsync(
                    newStoragePath,
                    cancellationToken);
            }
            catch
            {
                // The original database exception is more important.
            }

            throw;
        }

        /*
         * Delete the replaced object only after the database
         * successfully points to the new object.
         */
        if (!string.IsNullOrWhiteSpace(
                previousStoragePath) &&
            !string.Equals(
                previousStoragePath,
                newStoragePath,
                StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _storage.DeleteAsync(
                    previousStoragePath,
                    cancellationToken);
            }
            catch (Exception deleteException)
            {
                _logger.LogWarning(
                    deleteException,
                    "The old verification object could not be deleted after replacement. Path: {Path}",
                    previousStoragePath);
            }
        }

        return Ok(new
        {
            message =
                "Document uploaded successfully.",

            document = new
            {
                existingDocument.Id,
                existingDocument.DocumentType,
                existingDocument.OriginalFileName,
                existingDocument.ContentType,
                existingDocument.FileSizeBytes,
                existingDocument.VerificationStatus,
                existingDocument.UploadedAt
            }
        });
    }

    // POST /api/role-verifications/{id}/submit
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitApplication(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();

        var application =
            await _context.RoleVerificationApplications
                .Include(item =>
                    item.Documents)
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == id &&
                        item.UserId == userId,
                    cancellationToken);

        if (application == null)
        {
            return NotFound(new
            {
                message =
                    "Verification application was not found."
            });
        }

        if (application.Status is
            "under_review" or
            "active" or
            "approved")
        {
            return BadRequest(new
            {
                message =
                    "This application has already been submitted or approved."
            });
        }

        var requiredDocuments =
            GetRequiredDocuments(
                application.RoleKey);

        var uploadedTypes =
            application.Documents
                .Where(document =>
                    !string.IsNullOrWhiteSpace(
                        document.StoragePath))
                .Select(document =>
                    document.DocumentType)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var missingDocuments =
            requiredDocuments
                .Where(required =>
                    !uploadedTypes.Contains(required))
                .ToList();

        if (missingDocuments.Count > 0)
        {
            return BadRequest(new
            {
                message =
                    "Upload every required document before submitting.",

                missingDocuments
            });
        }

        var now = DateTime.UtcNow;

        application.Status =
            "under_review";

        application.SubmittedAt = now;
        application.ReviewerNotes = null;
        application.RejectionReason = null;
        application.UpdatedAt = now;

        var userRole =
            await _context.UserRoles
                .FirstOrDefaultAsync(
                    role =>
                        role.UserId ==
                            application.UserId &&
                        role.RoleKey ==
                            application.RoleKey,
                    cancellationToken);

        if (userRole != null)
        {
            userRole.Status =
                "under_review";

            userRole.ActivatedAt = null;
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(new
        {
            message =
                "Verification application submitted for review.",

            application.Id,
            application.RoleKey,
            application.Status,
            application.SubmittedAt
        });
    }

    private Guid GetRequiredUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            throw new UnauthorizedAccessException(
                "The authenticated user ID is invalid.");
        }

        return userId;
    }

    private static string NormalizeRole(
        string value)
    {
        var normalized =
            value
                .Trim()
                .ToLowerInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");

        return normalized switch
        {
            "provider" => "supplier",
            "rider" => "driver",
            "motorcycle_rider" => "driver",
            "auto_parts_store" => "supplier",
            _ => normalized
        };
    }

    private static string NormalizeDocumentType(
        string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");
    }

    private static string? CleanOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool IsRequiredDocument(
        string roleKey,
        string documentType)
    {
        return GetRequiredDocuments(roleKey)
            .Contains(
                documentType,
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string>
        GetRequiredDocuments(
            string roleKey)
    {
        return NormalizeRole(roleKey) switch
        {
            "driver" =>
                new[]
                {
                    "government_id",
                    "drivers_license",
                    "vehicle_registration",
                    "vehicle_photo",
                    "selfie_with_id"
                },

            "mechanic" =>
                new[]
                {
                    "government_id",
                    "workplace_photo",
                    "professional_proof",
                    "selfie_with_id"
                },

            "supplier" =>
                new[]
                {
                    "government_id",
                    "business_registration",
                    "storefront_photo",
                    "business_address_proof"
                },

            _ =>
                Array.Empty<string>()
        };
    }
}
