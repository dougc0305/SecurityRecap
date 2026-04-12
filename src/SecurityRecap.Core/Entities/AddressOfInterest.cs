namespace SecurityRecap.Core.Entities;

public class AddressOfInterest
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? Label { get; set; }
    public int IncidentCount { get; set; }
    public DateTime? FirstFlagged { get; set; }
    public DateTime? LastIncident { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
}
