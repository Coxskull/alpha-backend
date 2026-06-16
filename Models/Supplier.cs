namespace Alpha.API.Models;

public class Supplier
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string? ContactNumber { get; set; }

    public string? Address { get; set; }

    public string AvailabilityStatus { get; set; } = "available";

    public DateTime CreatedAt { get; set; }
    public string? Territory { get; set; }

    public int CurrentWorkload { get; set; } = 0;
    public string Territory { get; set; } = "";

    public int CurrentWorkload { get; set; }

    public double ResponseRate { get; set; }
}