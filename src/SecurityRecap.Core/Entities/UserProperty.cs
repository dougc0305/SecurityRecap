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

    /// <summary>
    /// Whether this user should be emailed when report pickup fails, or when a report that
    /// was expected has not arrived. Separate from the summary flag: board members want the
    /// nightly report, but usually only an operator wants to hear that the plumbing broke.
    /// </summary>
    public bool ReceivesAlerts { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
