namespace SecurityRecap.Core.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? PlateState { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Color { get; set; }
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    public int ViolationCount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
