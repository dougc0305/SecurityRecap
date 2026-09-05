namespace SecurityRecap.Core.Interfaces;

public record MailboxMessageOutcome(
    string Subject,
    string FromAddress,
    DateTime ReceivedAtUtc,
    string? FileName,
    Guid? ReportId,
    bool AlreadyIngested,
    bool SummaryEmailSent,
    string? Error);

public record MailboxPollResult(
    Guid PropertyId,
    string PropertyName,
    int MessagesExamined,
    int ReportsIngested,
    int MessagesSkipped,
    IReadOnlyList<MailboxMessageOutcome> Messages,
    string? Error)
{
    public bool Succeeded => Error is null;
}

public interface IMailboxIngestionService
{
    /// <summary>
    /// Scans the configured mailbox for the given property, ingests any matching report PDFs,
    /// and sends the generated summary if the property is configured to. Safe to call
    /// concurrently with the background poller: ingestion is idempotent per message.
    /// </summary>
    Task<MailboxPollResult> PollPropertyAsync(Guid propertyId, CancellationToken ct = default);

    /// <summary>Property ids with an enabled mailbox config whose poll interval has elapsed.</summary>
    Task<IReadOnlyList<Guid>> GetPropertiesDueForPollAsync(CancellationToken ct = default);

    /// <summary>Confirms the stored credentials can reach the mailbox. Returns its display name.</summary>
    Task<string> VerifyConfigurationAsync(Guid propertyId, CancellationToken ct = default);
}
