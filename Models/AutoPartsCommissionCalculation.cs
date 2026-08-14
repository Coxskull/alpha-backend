using System;

namespace Alpha.API.Models;

public class AutoPartsCommissionCalculation
{
	public Guid Id { get; set; }

	public Guid OrderId { get; set; }

	public Guid? OrderFinancialId { get; set; }

	public Guid PolicyId { get; set; }

	public int PolicyVersion { get; set; }

	public string Currency { get; set; } = "USD";

	public decimal PartsSubtotal { get; set; }

	public decimal TotalCommission { get; set; }

	public decimal EffectiveCommissionRate { get; set; }

	public DateTime CalculatedAt { get; set; }

	public string CreatedBy { get; set; } = "system";

	public AutoPartsCommissionPolicy? Policy { get; set; }
}