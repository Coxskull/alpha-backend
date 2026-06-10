using System.ComponentModel.DataAnnotations;

namespace Alpha.API.Models;
public class User { 

    [Key] 
    public Guid Id { get; set; } 
    public string FullName { get; set; } = string.Empty; 
    public string Email { get; set; } = string.Empty; 
    public string? Phone { get; set; } 
    public string Role { get; set; } = "customer"; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
}