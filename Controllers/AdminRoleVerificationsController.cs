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

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/admin/role-verifications")]
[Authorize(Roles = "admin")]
public class AdminRoleVerificationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminRoleVerificationsController> _logger;

    public AdminRoleVerificationsController(
        AppDbContext context,
        IWebHostEnvironment environment,
        ILogger<AdminRoleVerificationsController> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
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

        var fullPath =
            ResolveDocumentPath(document.FilePath);

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new
            {
                message =
                    "The document file could not be found."
            });
        }

        var contentType =
            string.IsNullOrWhiteSpace(
                document.ContentType)
                ? "application/octet-stream"
                : document.ContentType;

        var bytes =
            await System.IO.File.ReadAllBytesAsync(
                fullPath,
                cancellationToken);

        return File(
            bytes,
            contentType,
            document.OriginalFileName);
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

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveApplication(
        Guid id,
        [FromBody] AdminApproveVerificationDto dto,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var application =
                await _context.RoleVerificationApplications
                    .Include(item =>
                        item.Documents)
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

            if (application.Status != "under_review" &&
                application.Status !=
                    "needs_more_information")
            {
                return BadRequest(new
                {
                    message =
                        "Only applications under review can be approved."
                });
            }

            var documents =
                application.Documents?.ToList() ??
                new List<RoleVerificationDocument>();

            if (documents.Count == 0)
            {
                return BadRequest(new
                {
                    message =
                        "The application has no uploaded documents."
                });
            }

            var pendingDocuments =
                documents.Where(document =>
                    document.VerificationStatus !=
                    "accepted")
                .Select(document =>
                    document.OriginalFileName)
                .ToList();

            if (pendingDocuments.Count > 0)
            {
                return BadRequest(new
                {
                    message =
                        "Every required document must be accepted before approving the application.",

                    documents =
                        pendingDocuments
                });
            }

            var now = DateTime.UtcNow;
            var adminUserId =
                GetCurrentUserId();

            application.Status = "active";
            application.ReviewerNotes =
                string.IsNullOrWhiteSpace(
                    dto.ReviewerNotes)
                    ? null
                    : dto.ReviewerNotes.Trim();

            application.RejectionReason = null;
            application.ReviewedAt = now;
            application.ReviewedByUserId =
                adminUserId;
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

            if (userRole == null)
            {
                return BadRequest(new
                {
                    message =
                        "The user role record could not be found."
                });
            }

            userRole.Status = "active";
            userRole.ActivatedAt = now;

            await ActivateOperationalProfile(
                application.UserId,
                application.RoleKey,
                cancellationToken);

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return Ok(new
            {
                message =
                    $"{GetRoleTitle(application.RoleKey)} verification approved.",

                application.Id,
                application.RoleKey,
                status = application.Status
            });
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            _logger.LogError(
                exception,
                "Unable to approve verification application {ApplicationId}.",
                id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "Unable to approve the verification application.",

                    detail =
                        exception.Message
                });
        }
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
        if (string.IsNullOrWhiteSpace(
            dto.ReviewerNotes))
        {
            return BadRequest(new
            {
                message =
                    "Explain what information or documents are required."
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

        application.Status =
            "needs_more_information";

        application.ReviewerNotes =
            dto.ReviewerNotes.Trim();

        application.RejectionReason =
            null;

        application.ReviewedAt =
            DateTime.UtcNow;

        application.ReviewedByUserId =
            GetCurrentUserId();

        application.UpdatedAt =
            DateTime.UtcNow;

        await UpdateUserRoleStatus(
            application.UserId,
            application.RoleKey,
            "needs_more_information",
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);

        return Ok(new
        {
            message =
                "The applicant has been asked to provide more information."
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
        if (Path.IsPathRooted(storedPath))
        {
            return storedPath;
        }

        var relativePath = storedPath
            .TrimStart('/', '\\')
            .Replace(
                '/',
                Path.DirectorySeparatorChar);

        return Path.Combine(
            _environment.ContentRootPath,
            relativePath);
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