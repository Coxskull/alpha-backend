using System.ComponentModel.DataAnnotations;

namespace Alpha.API.DTOs;

public class RegisterDto
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ReferralCode { get; set; }

    [Required]
    [MinLength(1)]
    public List<string> SelectedRoles { get; set; } = new();

    [MaxLength(50)]
    public string? PrimaryRole { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? State { get; set; }

    [Required]
    [MaxLength(2)]
    public string Country { get; set; } = "MX";

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "es";

    [MaxLength(200)]
    public string? BusinessName { get; set; }

    [MaxLength(1000)]
    public string? EntrepreneurialGoal { get; set; }

    [Required]
    public bool AcceptTerms { get; set; }

    [Required]
    public bool AcceptRewardsPolicy { get; set; }
}