using System;

namespace Alpha.API.Models
{
    public class DriverPayout
    {
        public Guid Id { get; set; }

        public Guid DriverId { get; set; }

        public Guid OrderId { get; set; }

        public decimal Amount { get; set; }

        public string Status { get; set; } = "pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidAt { get; set; }
    }
}