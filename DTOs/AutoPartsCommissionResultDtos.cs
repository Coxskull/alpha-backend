using System;
using System.Collections.Generic;

namespace Alpha.API.DTOs
{
    public class AutoPartsCommissionResultDtos
    {
        public Guid PolicyId { get; set; }

        public int PolicyVersion { get; set; }

        public string Currency { get; set; } = "USD";

        public decimal PartsSubtotal { get; set; }

        public decimal TotalCommission { get; set; }

        public decimal EffectiveCommissionRate { get; set; }

        public decimal SupplierNet { get; set; }

        public List<AutoPartsCommissionLineResultDtos> Lines { get; set; }
            = new();
    }

    public class AutoPartsCommissionLineResultDtos
    {
        public Guid TierId { get; set; }

        public int TierOrder { get; set; }

        public decimal TierMinimum { get; set; }

        public decimal? TierMaximum { get; set; }

        public decimal TierPercentage { get; set; }

        public decimal AmountInTier { get; set; }

        public decimal CommissionAmount { get; set; }
    }
}