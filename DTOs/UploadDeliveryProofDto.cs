using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Alpha.API.DTOs;

public class UploadDeliveryProofDto
{
    [Required]
    public IFormFile Image { get; set; } = default!;
}