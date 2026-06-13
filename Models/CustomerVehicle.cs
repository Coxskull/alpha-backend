using System;
using System.ComponentModel.DataAnnotations;

namespace Alpha.API.Models;

public class CustomerVehicle
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    public int Year { get; set; }

    [Required]
    [MaxLength(100)]
    public string Make { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Trim { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Engine { get; set; } = string.Empty;

    [MaxLength(50)]
    public string VIN { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Nickname { get; set; } = string.Empty;

    public bool IsPrimary { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}