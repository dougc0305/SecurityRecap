namespace SecurityRecap.Core.Entities;

using SecurityRecap.Core.Enums;

public class ServiceAssignment
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public ServiceRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}
