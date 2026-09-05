namespace SecurityRecap.Core.Entities;

public class UserProperty
{
    public Guid UserId { get; set; }
    public Guid PropertyId { get; set; }

    /// <summary>
    /// Whether this user should be emailed the generated summary for this property.
    /// Held here rather than on the user so someone can receive reports for one property
    /// and not another, and so losing access to a property stops the mail with it.
    /// </summary>
    public bool ReceivesSummary { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
