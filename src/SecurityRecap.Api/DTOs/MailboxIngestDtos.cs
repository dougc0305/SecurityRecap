using System.ComponentModel.DataAnnotations;

namespace SecurityRecap.Api.DTOs;

/// <summary>Mailbox configuration as returned to the UI. Never carries the client secret.</summary>
public record MailboxIngestConfigDto(
    Guid PropertyId,
    string GraphTenantId,
    string GraphClientId,
    bool HasClientSecret,
    string MailboxAddress,
    string FolderName,
    string? FromAddress,
    string? SubjectContains,
    string? AttachmentNameContains,
    int LookbackDays,
    int PollIntervalMinutes,
    TimeOnly? ActiveWindowStart,
    TimeOnly? ActiveWindowEnd,
    int ActiveWindowPollMinutes,
    string? ScheduleTimeZone,
    int StaleAfterHours,
    bool MarkAsRead,
    string? MoveToFolder,
    bool SendSummaryEmail,
    string[] SummaryRecipients,
    string? SummarySubjectPrefix,
    bool IsEnabled,
    DateTime? LastPolledAt,
    DateTime? LastSuccessAt,
    DateTime? LastMessageReceivedAt,
    string? LastError,
    int ConsecutiveFailures,
    DateTime? LastReportIngestedAt,
    DateTime? LastAlertAt);

public class SaveMailboxIngestConfigRequest
{
    [Required]
    public Guid PropertyId { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Graph tenant id is required")]
    public string GraphTenantId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Graph client id is required")]
    public string GraphClientId { get; set; } = string.Empty;

    /// <summary>
    /// Leave null to keep the stored secret; supply a value to replace it.
    /// The stored secret is never sent back to the client, so an edit form
    /// that round-trips the DTO will correctly leave this null.
    /// </summary>
    public string? GraphClientSecret { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Mailbox address is required")]
    [EmailAddress(ErrorMessage = "Mailbox address must be a valid email address")]
    public string MailboxAddress { get; set; } = string.Empty;

    public string FolderName { get; set; } = "inbox";

    [EmailAddress(ErrorMessage = "Sender filter must be a valid email address")]
    public string? FromAddress { get; set; }

    public string? SubjectContains { get; set; }
    public string? AttachmentNameContains { get; set; }

    [Range(1, 90, ErrorMessage = "Lookback must be between 1 and 90 days")]
    public int LookbackDays { get; set; } = 3;

    [Range(1, 1440, ErrorMessage = "Poll interval must be between 1 minute and 24 hours")]
    public int PollIntervalMinutes { get; set; } = 15;

    /// <summary>Start of the daily window when the report is expected, in ScheduleTimeZone.</summary>
    public TimeOnly? ActiveWindowStart { get; set; }

    /// <summary>End of that window. May wrap past midnight.</summary>
    public TimeOnly? ActiveWindowEnd { get; set; }

    [Range(1, 1440, ErrorMessage = "Active window interval must be between 1 minute and 24 hours")]
    public int ActiveWindowPollMinutes { get; set; } = 5;

    /// <summary>IANA timezone for the window. Falls back to the property's timezone when blank.</summary>
    public string? ScheduleTimeZone { get; set; }

    /// <summary>Alert when no report has arrived for this many hours. Zero disables the check.</summary>
    [Range(0, 720, ErrorMessage = "Stale threshold must be between 0 and 720 hours")]
    public int StaleAfterHours { get; set; } = 26;

    public bool MarkAsRead { get; set; } = true;
    public string? MoveToFolder { get; set; }

    public bool SendSummaryEmail { get; set; }
    public string[] SummaryRecipients { get; set; } = Array.Empty<string>();
    public string? SummarySubjectPrefix { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public record MailboxMessageOutcomeDto(
    string Subject,
    string FromAddress,
    DateTime ReceivedAtUtc,
    string? FileName,
    Guid? ReportId,
    bool AlreadyIngested,
    bool SummaryEmailSent,
    string? Error);

public record MailboxPollResultDto(
    Guid PropertyId,
    string PropertyName,
    bool Succeeded,
    int MessagesExamined,
    int ReportsIngested,
    int MessagesSkipped,
    IReadOnlyList<MailboxMessageOutcomeDto> Messages,
    string? Error);

public record MailboxVerifyResultDto(string Mailbox);
