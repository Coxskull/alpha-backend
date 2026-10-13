using System.ComponentModel.DataAnnotations;
namespace Alpha.API.DTOs;
public sealed class FinancialConflictReviewDto
{
    [Required, StringLength(128)] public string IdempotencyKey { get; set; } = "";
    [Required, StringLength(4000)] public string Evidence { get; set; } = "";
}
public sealed class FinancialConflictApprovalDto
{
    [Required, StringLength(4000)] public string Evidence { get; set; } = "";
}
