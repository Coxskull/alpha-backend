using System.ComponentModel.DataAnnotations;

namespace Alpha.API.DTOs;

public class SubmitVerificationDto
{
    [Required]
    public string RoleKey { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(200)]
    public string? BusinessName { get; set; }

    [MaxLength(100)]
    public string? IdentificationNumber { get; set; }

    [MaxLength(100)]
    public string? LicenseNumber { get; set; }

    [MaxLength(50)]
    public string? VehiclePlateNumber { get; set; }

    [Range(0, 80)]
    public int? YearsOfExperience { get; set; }

    [MaxLength(500)]
    public string? BusinessAddress { get; set; }

    [MaxLength(2000)]
    public string? ApplicantNotes { get; set; }
}

public class UploadVerificationDocumentDto
{
    [Required]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    public IFormFile File { get; set; } = null!;
}

public class ReviewVerificationDto
{
    [Required]
    public string Decision { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ReviewerNotes { get; set; }

    [MaxLength(2000)]
    public string? RejectionReason { get; set; }
}