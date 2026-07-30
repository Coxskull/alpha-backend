using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("role_verification_applications")]
public class RoleVerificationApplication
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("role_key")]
    public string RoleKey { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "draft";

    [Column("legal_name")]
    public string? LegalName { get; set; }

    [Column("business_name")]
    public string? BusinessName { get; set; }

    [Column("identification_number")]
    public string? IdentificationNumber { get; set; }

    [Column("license_number")]
    public string? LicenseNumber { get; set; }

    [Column("vehicle_plate_number")]
    public string? VehiclePlateNumber { get; set; }

    [Column("years_of_experience")]
    public int? YearsOfExperience { get; set; }

    [Column("business_address")]
    public string? BusinessAddress { get; set; }

    [Column("applicant_notes")]
    public string? ApplicantNotes { get; set; }

    [Column("reviewer_notes")]
    public string? ReviewerNotes { get; set; }

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by_user_id")]
    public Guid? ReviewedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } =
        DateTime.UtcNow;

    public User User { get; set; } = null!;

    public ICollection<RoleVerificationDocument> Documents { get; set; } =
        new List<RoleVerificationDocument>();
}