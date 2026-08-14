using System;

namespace Alpha.API.Models
{
    public class AutoPartsCommissionTier
    {
        public Guid Id { get; set; }

        public Guid PolicyId { get; set; }

        public int TierOrder { get; set; }

        public decimal MinimumAmount { get; set; }

        public decimal? MaximumAmount { get; set; }

        public decimal CommissionPercentage { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public AutoPartsCommissionPolicy? Policy { get; set; }
    }
}