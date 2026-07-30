using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("role_verification_documents")]
public class RoleVerificationDocument
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("application_id")]
    public Guid ApplicationId { get; set; }

    [Required]
    [Column("document_type")]
    public string DocumentType { get; set; } =
        string.Empty;

    [Required]
    [Column("original_file_name")]
    public string OriginalFileName { get; set; } =
        string.Empty;

    /*
     * Existing database column.
     *
     * This may contain the Supabase Storage object path,
     * for example:
     *
     * role-verifications/{applicationId}/{fileName}
     */
    [Required]
    [Column("storage_path")]
    public string StoragePath { get; set; } =
        string.Empty;

    /*
     * Optional local file-system path.
     *
     * This column already exists in your database.
     * It may be null when documents are stored in
     * Supabase Storage instead of Railway's local disk.
     */
    [Column("file_path")]
    public string? FilePath { get; set; }

    [Column("content_type")]
    public string? ContentType { get; set; }

    [Column("file_size_bytes")]
    public long? FileSizeBytes { get; set; }

    [Required]
    [Column("verification_status")]
    public string VerificationStatus { get; set; } =
        "pending";

    [Column("reviewer_notes")]
    public string? ReviewerNotes { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } =
        DateTime.UtcNow;

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by_user_id")]
    public Guid? ReviewedByUserId { get; set; }

    [ForeignKey(nameof(ApplicationId))]
    public RoleVerificationApplication? Application
    {
        get;
        set;
    }
}