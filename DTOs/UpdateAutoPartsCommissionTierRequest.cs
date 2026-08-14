namespace Alpha.API.DTOs;

public class UpdateAutoPartsCommissionTierRequest
{
    public decimal Minimum { get; set; }

    public decimal? Maximum { get; set; }

    public decimal CommissionRate { get; set; }

    public bool IsActive { get; set; }
}