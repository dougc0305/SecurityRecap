namespace SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public PlanType Plan { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
