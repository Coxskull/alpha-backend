using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("delivery_proof")]
public class DeliveryProof
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("image_url")]
    public string ImageUrl { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }
}