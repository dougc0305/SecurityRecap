namespace SecurityRecap.Core.Interfaces;

/// <summary>Credentials and targeting for a single mailbox, resolved from a MailboxIngestConfig.</summary>
public record MailboxCredentials(
    string TenantId,
    string ClientId,
    string ClientSecret,
    string MailboxAddress);

public record MailboxQuery(
    string FolderName,
    string? FromAddress,
    string? SubjectContains,
    /// <summary>
    /// Required, not optional: it is the only server-side restriction, and it doubles as the
    /// sort key. Without it a request would return the oldest mail in the mailbox.
    /// </summary>
    DateTime ReceivedAfterUtc,
    int MaxMessages = 50);

public record MailboxAttachment(string Name, byte[] Content);

/// <summary>
/// Attachment metadata without the bytes. Listing is cheap; downloading a report PDF is not,
/// so the two are separate calls and the caller decides whether the content is worth fetching.
/// </summary>
public record MailboxAttachmentInfo(string Id, string Name, long Size);

public record MailboxMessage(
    string Id,
    string InternetMessageId,
    string Subject,
    string FromAddress,
    DateTime ReceivedAtUtc,
    IReadOnlyList<MailboxAttachment> PdfAttachments);

/// <summary>
/// Reads report emails out of a mailbox and sends the generated summary back out from it.
/// Both halves use the same app-only credential, so they live on one abstraction.
/// </summary>
public interface IMailboxClient
{
    /// <summary>
    /// Verifies the credentials can actually read the target folder, and returns a description
    /// of it. Deliberately exercises the same permission polling needs, so a successful test
    /// means polling will work. Throws MailboxException on failure.
    /// </summary>
    Task<string> VerifyAccessAsync(MailboxCredentials credentials, string folderName, CancellationToken ct = default);

    Task<IReadOnlyList<MailboxMessage>> FetchMessagesAsync(
        MailboxCredentials credentials, MailboxQuery query, CancellationToken ct = default);

    /// <summary>
    /// Lists a message's PDF attachments without transferring their content.
    /// </summary>
    Task<IReadOnlyList<MailboxAttachmentInfo>> ListPdfAttachmentsAsync(
        MailboxCredentials credentials, string messageId, string? nameContains, CancellationToken ct = default);

    /// <summary>
    /// Downloads one attachment's bytes. Kept apart from listing so the caller can skip the
    /// transfer for an attachment it has already ingested.
    /// </summary>
    Task<MailboxAttachment> DownloadAttachmentAsync(
        MailboxCredentials credentials, string messageId, MailboxAttachmentInfo attachment, CancellationToken ct = default);

    Task MarkAsReadAsync(MailboxCredentials credentials, string messageId, CancellationToken ct = default);

    Task MoveAsync(MailboxCredentials credentials, string messageId, string destinationFolder, CancellationToken ct = default);

    Task SendMailAsync(
        MailboxCredentials credentials,
        IReadOnlyList<string> to,
        string subject,
        string htmlBody,
        IReadOnlyList<MailboxAttachment>? attachments = null,
        CancellationToken ct = default);
}
