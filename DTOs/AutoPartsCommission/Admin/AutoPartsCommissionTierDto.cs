using System;

namespace Alpha.API.DTOs.AutoPartsCommission.Admin;

public class AutoPartsCommissionTierDto
{
    public Guid Id { get; set; }

    public int TierOrder { get; set; }

    public decimal MinimumAmount { get; set; }

    public decimal? MaximumAmount { get; set; }

    public decimal CommissionPercentage { get; set; }

    public bool IsActive { get; set; }
}