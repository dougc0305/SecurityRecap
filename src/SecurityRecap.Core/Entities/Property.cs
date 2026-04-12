namespace SecurityRecap.Core.Entities;

public class Property
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
    public string? SecurityCompany { get; set; }
    public string? ReportEmail { get; set; }
    public string Timezone { get; set; } = "America/New_York";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<AddressOfInterest> AddressesOfInterest { get; set; } = new List<AddressOfInterest>();
    public ICollection<UserProperty> UserProperties { get; set; } = new List<UserProperty>();
}
