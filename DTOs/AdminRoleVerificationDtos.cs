namespace Alpha.API.DTOs;

public class AdminReviewDocumentDto
{
	public string Status { get; set; } = string.Empty;

	public string? ReviewerNotes { get; set; }
}

public class AdminApproveVerificationDto
{
	public string? ReviewerNotes { get; set; }
}

public class AdminRejectVerificationDto
{
	public string Reason { get; set; } = string.Empty;

	public string? ReviewerNotes { get; set; }
}

public class AdminRequestMoreInformationDto
{
	public string ReviewerNotes { get; set; } = string.Empty;
}