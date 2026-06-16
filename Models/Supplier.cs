namespace Alpha.API.Models;

public class Supplier
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string? ContactNumber { get; set; }

    public string? Address { get; set; }

    public string AvailabilityStatus { get; set; } = "available";

    public string Territory { get; set; } = "";

    public int CurrentWorkload { get; set; } = 0;

    public double ResponseRate { get; set; } = 100;

    public DateTime CreatedAt { get; set; }
}