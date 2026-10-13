namespace Alpha.API.DTOs;

public sealed class RefundAllocationDto
{
    public string Evidence { get; set; } = string.Empty;
    public decimal PlatformAmount { get; set; }
    public List<RefundAllocationLineDto> Lines { get; set; } = new();
}
public sealed class RefundAllocationLineDto
{
    public string SourceKind { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public decimal Amount { get; set; }
}
public sealed class PayoutConfirmationDto
{
    public decimal? PaidAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PayoutReference { get; set; } = string.Empty;
}
