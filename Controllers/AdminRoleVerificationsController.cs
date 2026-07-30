using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Alpha.API.Services;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/admin/role-verifications")]
[Authorize(Roles = "admin")]
public class AdminRoleVerificationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminRoleVerificationsController> _logger;
    private readonly VerificationStorageService _storage;

    public AdminRoleVerificationsController(
    AppDbContext context,
    IWebHostEnvironment environment,
    ILogger<AdminRoleVerificationsController> logger,
    VerificationStorageService storage)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
        _storage = storage;
    }

    // ---------------------------------------------------------
    // GET: /api/admin/role-verifications
    // ---------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> GetApplications(
        [FromQuery] string? status,
        [FromQuery] string? role,
        CancellationToken cancellationToken)
    {
        var query = _context.RoleVerificationApplications
            .AsNoTracking()
            .Include(application => application.User)
            .Include(application => application.Documents)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus =
                NormalizeStatus(status);

            query = query.Where(application =>
                application.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole =
                NormalizeRole(role);

            query = query.Where(application =>
                application.RoleKey == normalizedRole);
        }

        var applications = await query
            .OrderBy(application =>
                application.Status == "under_review"
                    ? 0
                    : 1)
            .ThenByDescending(application =>
                application.SubmittedAt ??
                application.CreatedAt)
            .Select(application => new
            {
                application.Id,
                application.UserId,
                application.RoleKey,
                application.Status,

                application.LegalName,
                application.BusinessName,

                applicant = application.User == null
                    ? null
                    : new
                    {
                        application.User.Id,
                        application.User.FullName,
                        application.User.Email,
                        application.User.Phone
                    },

                documentCount =
                    application.Documents.Count,

                acceptedDocumentCount =
                    application.Documents.Count(
                        document =>
                            document.VerificationStatus ==
                            "accepted"),

                rejectedDocumentCount =
                    application.Documents.Count(
                        document =>
                            document.VerificationStatus ==
                            "rejected"),

                pendingDocumentCount =
                    application.Documents.Count(
                        document =>
                            document.VerificationStatus ==
                            "pending"),

                application.SubmittedAt,
                application.ReviewedAt,
                application.CreatedAt,
                application.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(applications);
    }

    // ---------------------------------------------------------
    // GET: /api/admin/role-verifications/{id}
    // ---------------------------------------------------------

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetApplication(
        Guid id,
        CancellationToken cancellationToken)
    {
        var application =
            await _context.RoleVerificationApplications
                .AsNoTracking()
                .Include(item => item.User)
                .Include(item => item.Documents)
                .FirstOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);

        if (application == null)
        {
            return NotFound(new
            {
                message =
                    "Verification application was not found."
            });
        }

        return Ok(new
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
            application.ReviewedByUserId,
            application.CreatedAt,
            application.UpdatedAt,

            applicant = application.User == null
                ? null
                : new
                {
                    application.User.Id,
                    application.User.FullName,
                    application.User.Email,
                    application.User.Phone
                },

            documents = application.Documents
                .OrderBy(document =>
                    document.UploadedAt)
                .Select(document => new
                {
                    document.Id,
                    document.DocumentType,
                    document.OriginalFileName,
                    document.StoragePath,
                    document.FilePath,
                    document.ContentType,
                    document.FileSizeBytes,
                    document.VerificationStatus,
                    document.ReviewerNotes,
                    document.ReviewedAt,
                    document.ReviewedByUserId,
                    document.UploadedAt
                })
        });
    }

    // ---------------------------------------------------------
    // GET document file
    // ---------------------------------------------------------

    [HttpGet("documents/{documentId:guid}/file")]
    public async Task<IActionResult> GetDocumentFile(
    Guid documentId,
    CancellationToken cancellationToken)
    {
        var document =
            await _context.RoleVerificationDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.Id == documentId,
                    cancellationToken);

        if (document == null)
        {
            return NotFound(new
            {
                message =
                    "Verification document was not found."
            });
        }

        if (string.IsNullOrWhiteSpace(
                document.StoragePath))
        {
            return NotFound(new
            {
                message =
                    "The document has no Supabase Storage path."
            });
        }

        try
        {
            var bytes =
                await _storage.DownloadAsync(
                    document.StoragePath,
                    cancellationToken);

            if (bytes.Length == 0)
            {
                return NotFound(new
                {
                    message =
                        "The stored document is empty."
                });
            }

            var contentType =
                string.IsNullOrWhiteSpace(
                    document.ContentType)
                    ? "application/octet-stream"
                    : document.ContentType.Trim();

            return File(
                bytes,
                contentType,
                document.OriginalFileName,
                enableRangeProcessing: true);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to download verification document {DocumentId} from Supabase Storage. Path: {StoragePath}",
                documentId,
                document.StoragePath);

            return NotFound(new
            {
                message =
                    "The document could not be found in secure storage."
            });
        }
    }

    // ---------------------------------------------------------
    // Review one document
    // ---------------------------------------------------------

    [HttpPost("documents/{documentId:guid}/review")]
    public async Task<IActionResult> ReviewDocument(
        Guid documentId,
        [FromBody] AdminReviewDocumentDto dto,
        CancellationToken cancellationToken)
    {
        var status =
            NormalizeStatus(dto.Status);

        if (status != "accepted" &&
            status != "rejected")
        {
            return BadRequest(new
            {
                message =
                    "Document status must be accepted or rejected."
            });
        }

        var document =
            await _context.RoleVerificationDocuments
                .Include(item =>
                    item.Application)
                .FirstOrDefaultAsync(
                    item => item.Id == documentId,
                    cancellationToken);

        if (document == null)
        {
            return NotFound(new
            {
                message =
                    "Verification document was not found."
            });
        }

        var adminUserId =
            GetCurrentUserId();

        document.VerificationStatus =
            status;

        document.ReviewerNotes =
            string.IsNullOrWhiteSpace(
                dto.ReviewerNotes)
                ? null
                : dto.ReviewerNotes.Trim();

        document.ReviewedAt =
            DateTime.UtcNow;

        document.ReviewedByUserId =
            adminUserId;

        if (document.Application != null)
        {
            document.Application.UpdatedAt =
                DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(new
        {
            message =
                $"Document marked as {status}.",

            document = new
            {
                document.Id,
                document.VerificationStatus,
                document.ReviewerNotes,
                document.ReviewedAt
            }
        });
    }

    // ---------------------------------------------------------
    // Approve entire application
    // ---------------------------------------------------------

    [HttpPost("{applicationId:guid}/approve")]
    public async Task<IActionResult> ApproveApplication(
     Guid applicationId,
     [FromBody] ReviewRoleVerificationDto? dto,
     CancellationToken cancellationToken)
    {
        var reviewerIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(
                reviewerIdValue,
                out var reviewerId))
        {
            return Unauthorized(new
            {
                message = "The admin user ID is invalid."
            });
        }

        var application =
            await _context.RoleVerificationApplications
                .Include(x => x.Documents)
                .FirstOrDefaultAsync(
                    x => x.Id == applicationId,
                    cancellationToken);

        if (application == null)
        {
            return NotFound(new
            {
                message =
                    "Verification application was not found."
            });
        }

        if (!string.Equals(
                application.Status,
                "under_review",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    $"Only applications under review can be approved. Current status: {application.Status}"
            });
        }

        var rejectedDocuments =
            application.Documents
                .Where(x =>
                    string.Equals(
                        x.VerificationStatus,
                        "rejected",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (rejectedDocuments.Count > 0)
        {
            return BadRequest(new
            {
                message =
                    "The application has rejected documents and cannot be approved."
            });
        }

        var pendingDocuments =
            application.Documents
                .Where(x =>
                    !string.Equals(
                        x.VerificationStatus,
                        "accepted",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (pendingDocuments.Count > 0)
        {
            return BadRequest(new
            {
                message =
                    "Every required document must be accepted before approving the application."
            });
        }

        var now = DateTime.UtcNow;

        application.Status = "approved";
        application.ReviewerNotes =
            string.IsNullOrWhiteSpace(dto?.ReviewerNotes)
                ? null
                : dto.ReviewerNotes.Trim();

        application.ReviewedByUserId = reviewerId;
        application.ReviewedAt = now;
        application.UpdatedAt = now;
        application.RejectionReason = null;

        foreach (var document in application.Documents)
        {
            document.VerificationStatus = "accepted";
            document.ReviewedByUserId = reviewerId;
            document.ReviewedAt ??= now;
        }

        var userRole =
            await _context.UserRoles
                .FirstOrDefaultAsync(
                    x =>
                        x.UserId == application.UserId &&
                        x.RoleKey == application.RoleKey,
                    cancellationToken);

        if (userRole == null)
        {
            userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = application.UserId,
                RoleKey = application.RoleKey,
                Status = "active",
                ActivatedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.UserRoles.Add(userRole);
        }
        else
        {
            userRole.Status = "active";
            userRole.ActivatedAt = now;
            userRole.UpdatedAt = now;
        }

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(
                exception,
                "Database error approving verification application {ApplicationId}.",
                applicationId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "A database error occurred while approving the application.",

                    detail =
                        exception.InnerException?.Message
                        ?? exception.Message
                });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected error approving verification application {ApplicationId}.",
                applicationId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "An unexpected error occurred while approving the application.",

                    detail = exception.Message
                });
        }

        return Ok(new
        {
            message =
                "Verification application approved successfully.",

            applicationId = application.Id,
            application.RoleKey,
            application.Status,
            application.ReviewedAt
        });
    }

    // ---------------------------------------------------------
    // Request additional information
    // ---------------------------------------------------------

    [HttpPost("{id:guid}/request-more-information")]
    public async Task<IActionResult>
    RequestMoreInformation(
        Guid id,
        [FromBody]
        AdminRequestMoreInformationDto dto,
        CancellationToken cancellationToken)
    {
        if (dto == null)
        {
            return BadRequest(new
            {
                message =
                    "Request information is required."
            });
        }

        var reviewerNotes =
            dto.ReviewerNotes?.Trim();

        if (string.IsNullOrWhiteSpace(
                reviewerNotes))
        {
            return BadRequest(new
            {
                message =
                    "Explain what information or documents are required."
            });
        }

        var reviewerId =
            GetCurrentUserId();

        var application =
            await _context
                .RoleVerificationApplications
                .Include(item =>
                    item.Documents)
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == id,
                    cancellationToken);

        if (application == null)
        {
            return NotFound(new
            {
                message =
                    "Verification application was not found."
            });
        }

        var allowedStatuses =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
            "under_review",
            "submitted",
            "pending",
            "needs_more_information"
            };

        if (!allowedStatuses.Contains(
                application.Status))
        {
            return BadRequest(new
            {
                message =
                    "More information cannot be requested " +
                    $"while the application status is " +
                    $"'{application.Status}'."
            });
        }

        var now =
            DateTime.UtcNow;

        application.Status =
            "needs_more_information";

        application.ReviewerNotes =
            reviewerNotes;

        application.RejectionReason =
            null;

        application.ReviewedAt =
            now;

        application.ReviewedByUserId =
            reviewerId;

        application.UpdatedAt =
            now;

        /*
         * Mark the documents as requiring more information.
         *
         * This allows the applicant page to clearly identify
         * which uploaded documents need to be replaced.
         *
         * Remove this foreach block if the request should only
         * apply to the application fields and not the documents.
         */
        foreach (
            var document in application.Documents)
        {
            document.VerificationStatus =
                "needs_more_information";

            document.ReviewerNotes =
                reviewerNotes;

            document.ReviewedAt =
                now;

            document.ReviewedByUserId =
                reviewerId;
        }

        var userRole =
            await _context.UserRoles
                .FirstOrDefaultAsync(
                    item =>
                        item.UserId ==
                            application.UserId &&
                        item.RoleKey ==
                            application.RoleKey,
                    cancellationToken);

        if (userRole == null)
        {
            userRole =
                new UserRole
                {
                    Id =
                        Guid.NewGuid(),

                    UserId =
                        application.UserId,

                    RoleKey =
                        application.RoleKey,

                    Status =
                        "needs_more_information",

                    ActivatedAt =
                        null,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            _context.UserRoles.Add(
                userRole);
        }
        else
        {
            userRole.Status =
                "needs_more_information";

            userRole.ActivatedAt =
                null;

            userRole.UpdatedAt =
                now;
        }

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(
                exception,
                "Database error while requesting more " +
                "information for verification application " +
                "{ApplicationId}.",
                id);

            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                new
                {
                    message =
                        "A database error occurred while " +
                        "requesting more information.",

                    detail =
                        exception.InnerException?.Message
                        ?? exception.Message
                });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected error while requesting more " +
                "information for verification application " +
                "{ApplicationId}.",
                id);

            return StatusCode(
                StatusCodes
                    .Status500InternalServerError,
                new
                {
                    message =
                        "An unexpected error occurred while " +
                        "requesting more information.",

                    detail =
                        exception.Message
                });
        }

        return Ok(new
        {
            message =
                "The applicant has been asked to provide more information.",

            application = new
            {
                application.Id,
                application.UserId,
                application.RoleKey,
                application.Status,
                application.ReviewerNotes,
                application.ReviewedAt,
                application.ReviewedByUserId,
                application.UpdatedAt
            },

            documents =
                application.Documents
                    .Select(document => new
                    {
                        document.Id,
                        document.DocumentType,
                        document.OriginalFileName,
                        document.VerificationStatus,
                        document.ReviewerNotes,
                        document.ReviewedAt
                    })
                    .ToList()
        });
    }

    // ---------------------------------------------------------
    // Reject entire application
    // ---------------------------------------------------------

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> RejectApplication(
        Guid id,
        [FromBody] AdminRejectVerificationDto dto,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Reason))
        {
            return BadRequest(new
            {
                message =
                    "A rejection reason is required."
            });
        }

        var application =
            await _context.RoleVerificationApplications
                .FirstOrDefaultAsync(
                    item => item.Id == id,
                    cancellationToken);

        if (application == null)
        {
            return NotFound(new
            {
                message =
                    "Verification application was not found."
            });
        }

        application.Status = "rejected";
        application.RejectionReason =
            dto.Reason.Trim();

        application.ReviewerNotes =
            string.IsNullOrWhiteSpace(
                dto.ReviewerNotes)
                ? null
                : dto.ReviewerNotes.Trim();

        application.ReviewedAt =
            DateTime.UtcNow;

        application.ReviewedByUserId =
            GetCurrentUserId();

        application.UpdatedAt =
            DateTime.UtcNow;

        await UpdateUserRoleStatus(
            application.UserId,
            application.RoleKey,
            "rejected",
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(new
        {
            message =
                "The verification application was rejected."
        });
    }

    private async Task UpdateUserRoleStatus(
        Guid userId,
        string roleKey,
        string status,
        CancellationToken cancellationToken)
    {
        var userRole =
            await _context.UserRoles
                .FirstOrDefaultAsync(
                    role =>
                        role.UserId == userId &&
                        role.RoleKey == roleKey,
                    cancellationToken);

        if (userRole == null)
        {
            return;
        }

        userRole.Status = status;

        if (status != "active")
        {
            userRole.ActivatedAt = null;
        }
    }

    private async Task ActivateOperationalProfile(
        Guid userId,
        string roleKey,
        CancellationToken cancellationToken)
    {
        if (roleKey == "driver")
        {
            var driver =
                await _context.Drivers
                    .FirstOrDefaultAsync(
                        item =>
                            item.UserId == userId,
                        cancellationToken);

            if (driver != null)
            {
                driver.AvailabilityStatus =
                    "available";
            }

            return;
        }

        if (roleKey == "mechanic")
        {
            var mechanic =
                await _context.Mechanics
                    .FirstOrDefaultAsync(
                        item =>
                            item.UserId == userId,
                        cancellationToken);

            if (mechanic != null)
            {
                mechanic.AvailabilityStatus =
                    "available";
            }

            return;
        }

        if (roleKey == "supplier")
        {
            var supplier =
                await _context.Suppliers
                    .FirstOrDefaultAsync(
                        item =>
                            item.UserId == userId,
                        cancellationToken);

            if (supplier != null)
            {
                supplier.AvailabilityStatus =
                    "available";
            }
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        return Guid.TryParse(
            userIdValue,
            out var userId)
                ? userId
                : null;
    }

    private string ResolveDocumentPath(
        string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            throw new ArgumentException(
                "The document path is empty.",
                nameof(storedPath));
        }

        var decodedPath =
            Uri.UnescapeDataString(
                storedPath.Trim());

        if (Path.IsPathRooted(decodedPath))
        {
            return Path.GetFullPath(decodedPath);
        }

        var relativePath = decodedPath
            .TrimStart('/', '\\')
            .Replace(
                '/',
                Path.DirectorySeparatorChar)
            .Replace(
                '\\',
                Path.DirectorySeparatorChar);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    _environment.ContentRootPath,
                    relativePath));

        var contentRoot =
            Path.GetFullPath(
                _environment.ContentRootPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                contentRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The document path is outside the application content directory.");
        }

        return fullPath;
    }

    private static string NormalizeStatus(
        string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");
    }

    private static string NormalizeRole(
        string value)
    {
        var normalized =
            NormalizeStatus(value);

        return normalized switch
        {
            "provider" => "supplier",
            "rider" => "driver",
            "motorcycle_rider" => "driver",
            "auto_parts_store" => "supplier",
            _ => normalized
        };
    }

    private static string GetRoleTitle(
        string role)
    {
        return role switch
        {
            "driver" => "Driver",
            "mechanic" => "Mechanic",
            "supplier" => "Auto Parts Supplier",
            _ => "Role"
        };
    }
}