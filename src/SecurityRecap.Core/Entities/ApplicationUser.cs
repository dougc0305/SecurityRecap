namespace SecurityRecap.Core.Entities;
using Microsoft.AspNetCore.Identity;
using SecurityRecap.Core.Enums;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<UserProperty> UserProperties { get; set; } = new List<UserProperty>();
}
