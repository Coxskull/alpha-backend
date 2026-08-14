using System;
using System.Collections.Generic;

namespace Alpha.API.DTOs.AutoPartsCommission.Admin;

public class CreateAutoPartsCommissionPolicyDto
{
    public string PolicyName { get; set; }
        = "Auto Parts Commission Policy";

    public string Currency { get; set; }
        = "USD";

    public DateTime EffectiveFrom { get; set; }

    public string? Notes { get; set; }

    public List<CreateAutoPartsCommissionTierDto> Tiers { get; set; }
        = new();
}