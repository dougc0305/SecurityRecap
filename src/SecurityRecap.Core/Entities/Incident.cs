namespace SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

public class Incident
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public Guid PropertyId { get; set; }
    public DateTime? IncidentTime { get; set; }
    public IncidentType IncidentType { get; set; }
    public Severity Severity { get; set; }
    public string? Location { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? OfficerName { get; set; }
    public bool LawEnforcement { get; set; }
    public string? CaseNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Report Report { get; set; } = null!;
    public Property Property { get; set; } = null!;
    public ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
