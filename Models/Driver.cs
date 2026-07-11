using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("drivers")]
public class Driver
{
    [Key]
    [Column("Id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Required]
    [Column("FullName")]
    public string FullName { get; set; } = string.Empty;

    [Column("PhoneNumber")]
    public string? PhoneNumber { get; set; }

    [Column("VehicleType")]
    public string? VehicleType { get; set; }

    [Column("PlateNumber")]
    public string? PlateNumber { get; set; }

    [Required]
    [Column("AvailabilityStatus")]
    public string AvailabilityStatus { get; set; } = "available";

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("Territory")]
    public string? Territory { get; set; }

    [Column("ActiveJobs")]
    public int ActiveJobs { get; set; }

    [Column("ResponseRate")]
    public double ResponseRate { get; set; } = 100;

    [Column("LastSeenAt")]
    public DateTime? LastSeenAt { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    public User? User { get; set; }
}