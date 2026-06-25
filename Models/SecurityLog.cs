using System;

namespace Alpha.API.Models;

public class SecurityLog
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public string? Role { get; set; }

    public string Path { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public string? Message { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}