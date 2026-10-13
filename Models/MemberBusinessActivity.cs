using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("member_business_activity")]
public sealed class MemberBusinessActivity
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("completed_orders")]
    public int CompletedOrders { get; set; }

    [Column("completed_service_requests")]
    public int CompletedServiceRequests { get; set; }

    [Column("completed_deliveries")]
    public int CompletedDeliveries { get; set; }

    [Column("fulfilled_parts_orders")]
    public int FulfilledPartsOrders { get; set; }

    [Column("customer_purchases")]
    public int CustomerPurchases { get; set; }

    [Column("gross_transaction_value")]
    public decimal GrossTransactionValue { get; set; }

    [Column("first_business_activity_at")]
    public DateTime? FirstBusinessActivityAt { get; set; }

    [Column("last_business_activity_at")]
    public DateTime? LastBusinessActivityAt { get; set; }

    [Column("is_business_active")]
    public bool IsBusinessActive { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

}
