namespace SecurityRecap.Core.Entities;

public class UserProperty
{
    public Guid UserId { get; set; }
    public Guid PropertyId { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
