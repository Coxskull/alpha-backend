using System;
using System.Collections.Generic;

namespace Alpha.API.Models
{
    public class AutoPartsCommissionPolicy
    {
        public Guid Id { get; set; }

        public string PolicyName { get; set; } = string.Empty;

        public string Currency { get; set; } = "USD";

        public int Version { get; set; }

        public bool IsActive { get; set; }

        public DateTime EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<AutoPartsCommissionTier> Tiers { get; set; }
            = new List<AutoPartsCommissionTier>();
    }
}