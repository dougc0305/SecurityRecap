namespace SecurityRecap.Core.Entities;

/// <summary>
/// Per-property configuration for pulling daily patrol report PDFs straight out of a
/// mailbox, so an operator no longer has to download the attachment and upload it by hand.
/// </summary>
public class MailboxIngestConfig
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }

    // --- Microsoft Graph app-only credentials ---
    public string GraphTenantId { get; set; } = string.Empty;
    public string GraphClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret protected with ASP.NET Data Protection. Never returned over the API.
    /// </summary>
    public string GraphClientSecretProtected { get; set; } = string.Empty;

    /// <summary>Mailbox (UPN or object id) the report lands in, e.g. reports@example.com.</summary>
    public string MailboxAddress { get; set; } = string.Empty;

    /// <summary>Well-known folder name or folder id to scan. Defaults to the inbox.</summary>
    public string FolderName { get; set; } = "inbox";

    // --- Message matching ---
    /// <summary>Only consider messages from this sender address. Null means any sender.</summary>
    public string? FromAddress { get; set; }

    /// <summary>Only consider messages whose subject contains this text. Null means any subject.</summary>
    public string? SubjectContains { get; set; }

    /// <summary>Only take PDF attachments whose file name contains this text. Null means any PDF.</summary>
    public string? AttachmentNameContains { get; set; }

    /// <summary>Ignore anything older than this many days on the first run so we don't backfill years of mail.</summary>
    public int LookbackDays { get; set; } = 3;

    // --- Behaviour ---
    public int PollIntervalMinutes { get; set; } = 15;
    public bool MarkAsRead { get; set; } = true;

    /// <summary>Folder to move processed messages into. Null leaves them in place.</summary>
    public string? MoveToFolder { get; set; }

    public bool SendSummaryEmail { get; set; }

    /// <summary>Recipients of the generated summary email.</summary>
    public string[] SummaryRecipients { get; set; } = Array.Empty<string>();

    /// <summary>Subject prefix for the summary email, e.g. "Palm Cove Security Report Summary".</summary>
    public string? SummarySubjectPrefix { get; set; }

    public bool IsEnabled { get; set; } = true;

    // --- Observability ---
    public DateTime? LastPolledAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastMessageReceivedAt { get; set; }
    public string? LastError { get; set; }
    public int ConsecutiveFailures { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
}
