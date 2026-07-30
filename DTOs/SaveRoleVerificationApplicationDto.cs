namespace Alpha.API.DTOs;

public sealed class SaveRoleVerificationApplicationDto
{
    public string RoleKey { get; set; } =
        string.Empty;

    public string LegalName { get; set; } =
        string.Empty;

    public string? BusinessName { get; set; }
    public string? IdentificationNumber { get; set; }
    public string? LicenseNumber { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? BusinessAddress { get; set; }
    public string? ApplicantNotes { get; set; }
}
