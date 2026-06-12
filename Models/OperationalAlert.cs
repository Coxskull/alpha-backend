using System;

namespace Alpha.API.Models;

public class OperationalAlert
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string AlertType { get; set; } = "";

    public string Message { get; set; } = "";

    public bool Resolved { get; set; }

    public DateTime CreatedAt { get; set; }
}