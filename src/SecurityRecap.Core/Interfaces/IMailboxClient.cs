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
    DateTime? ReceivedAfterUtc,
    int MaxMessages = 25);

public record MailboxAttachment(string Name, byte[] Content);

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
    /// Downloads a message's PDF attachments. Separate from listing so a poll that matches
    /// nothing never pays to transfer attachment bytes.
    /// </summary>
    Task<IReadOnlyList<MailboxAttachment>> FetchPdfAttachmentsAsync(
        MailboxCredentials credentials, string messageId, string? nameContains, CancellationToken ct = default);

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
