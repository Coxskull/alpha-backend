using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("role_verification_documents")]
public class RoleVerificationDocument
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("application_id")]
    public Guid ApplicationId { get; set; }

    [Column("document_type")]
    public string DocumentType { get; set; } =
        string.Empty;

    [Column("original_file_name")]
    public string OriginalFileName { get; set; } =
        string.Empty;

    /*
     * Supabase Storage object path only.
     *
     * Example:
     * user-id/application-id/driver/government_id/file.png
     */
    [Column("storage_path")]
    public string StoragePath { get; set; } =
        string.Empty;

    /*
     * Kept only for compatibility with old Railway-local uploads.
     * New uploads must leave this null.
     */
    [Column("file_path")]
    public string? FilePath { get; set; }

    [Column("content_type")]
    public string? ContentType { get; set; }

    [Column("file_size_bytes")]
    public long? FileSizeBytes { get; set; }

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

    public RoleVerificationApplication? Application
    {
        get;
        set;
    }
}
