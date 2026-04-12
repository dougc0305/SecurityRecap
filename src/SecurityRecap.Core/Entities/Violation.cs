namespace SecurityRecap.Core.Entities;

public class Violation
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Guid? VehicleId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool NoticeIssued { get; set; }
    public bool TowNotified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Incident Incident { get; set; } = null!;
    public Vehicle? Vehicle { get; set; }
}
