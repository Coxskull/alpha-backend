using System.ComponentModel.DataAnnotations;

namespace Alpha.API.DTOs;

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class AdminCreateUserDto
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    // Role-specific information
    public string? Territory { get; set; }

    public string? Address { get; set; }

    public string? VehicleType { get; set; }

    public string? PlateNumber { get; set; }
}

public class AdminUpdateUserDto
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;
}

public class AdminResetPasswordDto
{
    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}