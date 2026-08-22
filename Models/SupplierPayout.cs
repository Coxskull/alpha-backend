using System;

namespace Alpha.API.Models
{
	public class SupplierPayout
	{
		public Guid Id { get; set; }

		public Guid SupplierId { get; set; }

		public Guid OrderId { get; set; }

		public decimal Amount { get; set; }

		public string Currency { get; set; } = "PHP";

		public string PayoutStatus { get; set; } = "pending";

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? PaidAt { get; set; }
	}
}