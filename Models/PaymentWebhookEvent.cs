using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Alpha.API.Models;

[Table("payment_webhook_events")]
public class PaymentWebhookEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("gateway")]
    public string Gateway { get; set; } = string.Empty;

    [Column("gateway_event_id")]
    public string GatewayEventId { get; set; } = string.Empty;

    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [Column("payload", TypeName = "jsonb")]
    public JsonDocument Payload { get; set; } = null!;

    [Column("processed")]
    public bool Processed { get; set; }

    [Column("processing_error")]
    public string? ProcessingError { get; set; }

    [Column("received_at")]
    public DateTime ReceivedAt { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }
}