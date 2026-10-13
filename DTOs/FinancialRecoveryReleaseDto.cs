using System.ComponentModel.DataAnnotations;
namespace Alpha.API.DTOs;
public sealed class FinancialRecoveryReleaseDto
{
    public Guid AllocationId { get; set; }
    [Required, StringLength(4000)] public string Evidence { get; set; } = "";
}
