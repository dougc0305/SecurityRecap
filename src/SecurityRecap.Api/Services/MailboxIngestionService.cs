using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Exceptions;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Api.Services;

/// <summary>
/// Finds the daily patrol report email, feeds its PDF through the normal ingestion pipeline,
/// and sends the generated summary on to the property's distribution list. This replaces the
/// manual download-and-upload step.
/// </summary>
public class MailboxIngestionService : IMailboxIngestionService
{
    /// <summary>
    /// Re-scan slightly before the last seen message so a message that arrives while a poll is
    /// mid-flight is not skipped. Duplicate work is prevented by report ExternalId, not by this window.
    /// </summary>
    private static readonly TimeSpan WatermarkOverlap = TimeSpan.FromMinutes(5);

    /// <summary>Marker for a message that matched the filters but had no report PDF; not a real error.</summary>
    private const string NoAttachmentNote = "No matching PDF attachment";

    private readonly AppDbContext _db;
    private readonly IMailboxClient _mailbox;
    private readonly IIngestionService _ingestion;
    private readonly ISecretProtector _secrets;
    private readonly MailboxAlertNotifier _alerts;
    private readonly ILogger<MailboxIngestionService> _logger;

    public MailboxIngestionService(
        AppDbContext db,
        IMailboxClient mailbox,
        IIngestionService ingestion,
        ISecretProtector secrets,
        MailboxAlertNotifier alerts,
        ILogger<MailboxIngestionService> logger)
    {
        _db = db;
        _mailbox = mailbox;
        _ingestion = ingestion;
        _secrets = secrets;
        _alerts = alerts;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> GetPropertiesDueForPollAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var candidates = await _db.MailboxIngestConfigs
            .Where(c => c.IsEnabled && c.Property.IsActive)
            .Select(c => new
            {
                c.PropertyId,
                c.LastPolledAt,
                c.PollIntervalMinutes,
                c.ActiveWindowStart,
                c.ActiveWindowEnd,
                c.ActiveWindowPollMinutes,
                c.ScheduleTimeZone,
                PropertyTimeZone = c.Property.Timezone,
                c.ConsecutiveFailures
            })
            .ToListAsync(ct);

        var due = new List<Guid>();
        foreach (var c in candidates)
        {
            var interval = EffectiveIntervalMinutes(
                now, c.ActiveWindowStart, c.ActiveWindowEnd, c.ActiveWindowPollMinutes,
                c.PollIntervalMinutes, c.ScheduleTimeZone ?? c.PropertyTimeZone);

            if (c.LastPolledAt is null
                || now >= c.LastPolledAt.Value.AddMinutes(NextDelay(interval, c.ConsecutiveFailures)))
            {
                due.Add(c.PropertyId);
            }
        }

        return due;
    }

    /// <summary>
    /// The report lands in a narrow daily window, so poll tightly around it and sparsely the
    /// rest of the day rather than paying the same rate around the clock.
    /// </summary>
    internal int EffectiveIntervalMinutes(
        DateTime nowUtc,
        TimeOnly? windowStart,
        TimeOnly? windowEnd,
        int windowIntervalMinutes,
        int offPeakIntervalMinutes,
        string? timeZoneId)
    {
        if (windowStart is null || windowEnd is null)
            return offPeakIntervalMinutes;

        var local = ToLocalTime(nowUtc, timeZoneId);
        if (local is null)
            return Math.Min(windowIntervalMinutes, offPeakIntervalMinutes);

        return IsInWindow(TimeOnly.FromDateTime(local.Value), windowStart.Value, windowEnd.Value)
            ? windowIntervalMinutes
            : offPeakIntervalMinutes;
    }

    /// <summary>Handles a window that wraps past midnight, e.g. 22:00 to 02:00.</summary>
    internal static bool IsInWindow(TimeOnly now, TimeOnly start, TimeOnly end) =>
        start <= end
            ? now >= start && now <= end
            : now >= start || now <= end;

    private DateTime? ToLocalTime(DateTime utc, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return null;

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // A bad timezone must not silently disable the tight window and delay the report;
            // fall back to the faster cadence and make the misconfiguration visible.
            _logger.LogWarning(ex,
                "Timezone '{TimeZoneId}' could not be resolved; polling at the active-window rate all day",
                timeZoneId);
            return null;
        }
    }

    /// <summary>
    /// Backs off after repeated failures so a misconfigured mailbox does not hammer Graph
    /// every interval, while a healthy config keeps its configured cadence.
    /// </summary>
    private static double NextDelay(int intervalMinutes, int consecutiveFailures)
    {
        var interval = Math.Max(1, intervalMinutes);
        if (consecutiveFailures <= 0) return interval;

        var multiplier = Math.Pow(2, Math.Min(consecutiveFailures, 5)); // cap at 32x
        return Math.Min(interval * multiplier, TimeSpan.FromHours(6).TotalMinutes);
    }

    public async Task<string> VerifyConfigurationAsync(Guid propertyId, CancellationToken ct = default)
    {
        var config = await LoadConfigAsync(propertyId, ct);
        var credentials = ResolveCredentials(config);
        return await _mailbox.VerifyAccessAsync(credentials, config.FolderName, ct);
    }

    public async Task<MailboxPollResult> PollPropertyAsync(Guid propertyId, CancellationToken ct = default)
    {
        var config = await _db.MailboxIngestConfigs
            .Include(c => c.Property)
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId, ct)
            ?? throw new KeyNotFoundException($"No mailbox configuration exists for property {propertyId}");

        var propertyName = config.Property.Name;
        var outcomes = new List<MailboxMessageOutcome>();

        config.LastPolledAt = DateTime.UtcNow;

        MailboxCredentials credentials;
        try
        {
            credentials = ResolveCredentials(config);
        }
        catch (Exception ex) when (ex is CryptographicException or MailboxException)
        {
            return await FailAsync(config, propertyName, DescribeCredentialFailure(ex), outcomes, null, ct);
        }

        var query = new MailboxQuery(
            config.FolderName,
            config.FromAddress,
            config.SubjectContains,
            ReceivedAfter(config));

        IReadOnlyList<MailboxMessage> messages;
        try
        {
            messages = await _mailbox.FetchMessagesAsync(credentials, query, ct);
        }
        catch (MailboxException ex)
        {
            return await FailAsync(config, propertyName, ex.Message, outcomes, credentials, ct);
        }

        var ingestedCount = 0;
        var skippedCount = 0;
        var latestSeen = config.LastMessageReceivedAt;

        // Messages arrive oldest first, and the watermark only advances across the unbroken
        // run of messages at the front that were fully handled. Once one fails, the watermark
        // stops there so the failure is retried next poll — otherwise a newer message
        // succeeding would move the watermark past an older one that never got ingested.
        // Re-examining the already-handled newer messages is cheap: ingestion short-circuits
        // on ExternalId before it reads the PDF or calls Claude.
        var watermarkStillAdvancing = true;

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();

            IReadOnlyList<MailboxAttachmentInfo> pdfs;
            try
            {
                pdfs = await _mailbox.ListPdfAttachmentsAsync(
                    credentials, message.Id, config.AttachmentNameContains, ct);
            }
            catch (MailboxException ex)
            {
                watermarkStillAdvancing = false;
                outcomes.Add(new MailboxMessageOutcome(
                    message.Subject, message.FromAddress, message.ReceivedAtUtc,
                    null, null, false, false, ex.Message));
                continue;
            }

            if (pdfs.Count == 0)
            {
                skippedCount++;
                // Not a failure: this message matched the filters but carried no report PDF,
                // so the watermark may pass it rather than re-examining it forever.
                if (watermarkStillAdvancing)
                    latestSeen = Max(latestSeen, message.ReceivedAtUtc);

                outcomes.Add(new MailboxMessageOutcome(
                    message.Subject, message.FromAddress, message.ReceivedAtUtc,
                    null, null, false, false, NoAttachmentNote));
                continue;
            }

            var allSucceeded = true;

            foreach (var pdf in pdfs)
            {
                var outcome = await IngestAttachmentAsync(config, message, pdf, credentials, ct);
                outcomes.Add(outcome);

                if (outcome.Error is not null)
                {
                    allSucceeded = false;
                    continue;
                }

                if (!outcome.AlreadyIngested) ingestedCount++;
            }

            if (!allSucceeded)
            {
                watermarkStillAdvancing = false;
                continue;
            }

            if (watermarkStillAdvancing)
                latestSeen = Max(latestSeen, message.ReceivedAtUtc);

            await ApplyPostProcessingAsync(config, credentials, message, ct);
        }

        // "No matching PDF attachment" is an expected outcome for unrelated mail, not an error
        // worth surfacing on the config row.
        var realErrors = outcomes
            .Where(o => o.Error is not null && o.Error != NoAttachmentNote)
            .Select(o => o.Error!)
            .ToList();

        config.LastMessageReceivedAt = latestSeen;
        config.LastSuccessAt = DateTime.UtcNow;
        config.LastError = realErrors.Count > 0 ? string.Join("; ", realErrors.Take(3)) : null;

        // The mailbox itself was reachable, so the connection-level backoff resets even if
        // individual reports failed to process.
        config.ConsecutiveFailures = 0;

        if (ingestedCount > 0)
            config.LastReportIngestedAt = DateTime.UtcNow;

        await RaiseOrClearAlertsAsync(config, credentials, realErrors, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Mailbox poll for {PropertyName}: {Examined} message(s) examined, {Ingested} report(s) ingested, {Skipped} skipped",
            propertyName, messages.Count, ingestedCount, skippedCount);

        return new MailboxPollResult(
            propertyId, propertyName, messages.Count, ingestedCount, skippedCount, outcomes, null);
    }

    // ---------------------------------------------------------------- per-attachment

    private async Task<MailboxMessageOutcome> IngestAttachmentAsync(
        MailboxIngestConfig config,
        MailboxMessage message,
        MailboxAttachmentInfo pdf,
        MailboxCredentials credentials,
        CancellationToken ct)
    {
        // A message can carry more than one report PDF, so the idempotency key has to be
        // per attachment rather than per message.
        var externalId = $"graph:{message.InternetMessageId}:{pdf.Name}";

        // The newest message stays inside the watermark window until a newer one arrives, so
        // it is re-listed on every poll. Checking here — before the download — keeps that from
        // re-transferring a multi-megabyte PDF every few minutes only for the ingestion layer
        // to recognise it and throw the bytes away.
        var alreadyIngestedId = await _db.Reports
            .Where(r => r.PropertyId == config.PropertyId && r.ExternalId == externalId)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);

        if (alreadyIngestedId != Guid.Empty)
        {
            _logger.LogDebug(
                "Attachment {FileName} on {Subject} is already report {ReportId}; skipping download",
                pdf.Name, message.Subject, alreadyIngestedId);

            return new MailboxMessageOutcome(
                message.Subject, message.FromAddress, message.ReceivedAtUtc,
                pdf.Name, alreadyIngestedId, true, false, null);
        }

        try
        {
            var downloaded = await _mailbox.DownloadAttachmentAsync(credentials, message.Id, pdf, ct);
            await using var stream = new MemoryStream(downloaded.Content);

            // The poller has no signed-in user. Admin role against the property's own tenant
            // gives it exactly the property it is configured for and nothing else.
            var result = await _ingestion.IngestReportAsync(
                config.Property.TenantId,
                Guid.Empty,
                UserRole.Admin,
                config.PropertyId,
                stream,
                pdf.Name,
                externalId);

            if (result.AlreadyIngested)
            {
                _logger.LogInformation(
                    "Message {Subject} already ingested as report {ReportId}; skipping",
                    message.Subject, result.ReportId);

                return new MailboxMessageOutcome(
                    message.Subject, message.FromAddress, message.ReceivedAtUtc,
                    pdf.Name, result.ReportId, true, false, null);
            }

            var emailSent = false;
            string? emailError = null;

            if (config.SendSummaryEmail)
            {
                try
                {
                    await SendSummaryEmailAsync(config, credentials, result, ct);
                    emailSent = true;
                }
                catch (MailboxException ex)
                {
                    // The report is ingested and safe in the database; a failed send must not
                    // undo that or cause the message to be reprocessed.
                    emailError = "Report ingested, but the summary email failed: " + ex.Message;
                    _logger.LogError(ex, "Summary email failed for report {ReportId}", result.ReportId);
                }
            }

            return new MailboxMessageOutcome(
                message.Subject, message.FromAddress, message.ReceivedAtUtc,
                pdf.Name, result.ReportId, false, emailSent, emailError);
        }
        catch (MailboxException ex)
        {
            _logger.LogError(ex, "Downloading {FileName} from {Subject} failed", pdf.Name, message.Subject);
            return new MailboxMessageOutcome(
                message.Subject, message.FromAddress, message.ReceivedAtUtc,
                pdf.Name, null, false, false, ex.Message);
        }
        catch (ClaudeApiException ex)
        {
            var detail = ClaudeFailureFormatter.ToUserMessage(ex);
            _logger.LogError(ex, "Claude analysis failed for {FileName} from {Subject}", pdf.Name, message.Subject);
            return new MailboxMessageOutcome(
                message.Subject, message.FromAddress, message.ReceivedAtUtc,
                pdf.Name, null, false, false, detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingesting {FileName} from {Subject} failed", pdf.Name, message.Subject);
            return new MailboxMessageOutcome(
                message.Subject, message.FromAddress, message.ReceivedAtUtc,
                pdf.Name, null, false, false, ex.Message);
        }
    }

    private async Task SendSummaryEmailAsync(
        MailboxIngestConfig config,
        MailboxCredentials credentials,
        IngestionOutcome result,
        CancellationToken ct)
    {
        var recipients = await ResolveRecipientsAsync(config, ct);

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Summary email is enabled for property {PropertyId} but no recipients resolved",
                config.PropertyId);
            return;
        }

        var prefix = string.IsNullOrWhiteSpace(config.SummarySubjectPrefix)
            ? $"{config.Property.Name} Security Report Summary"
            : config.SummarySubjectPrefix;

        var dateLabel = result.ReportDate?.ToString("MMMM d, yyyy") ?? DateTime.UtcNow.ToString("MMMM d, yyyy");
        var subject = $"{prefix} - {dateLabel}";

        var attachments = new List<MailboxAttachment>();
        if (!string.IsNullOrWhiteSpace(result.MarkdownSummary))
        {
            attachments.Add(new MailboxAttachment(
                $"summary-{result.ReportDate:yyyy-MM-dd}.md",
                Encoding.UTF8.GetBytes(result.MarkdownSummary)));
        }

        await _mailbox.SendMailAsync(
            credentials, recipients, subject, BuildEmailBody(result), attachments, ct);

        _logger.LogInformation(
            "Sent summary for report {ReportId} to {RecipientCount} recipient(s)",
            result.ReportId, recipients.Count);
    }

    /// <summary>
    /// Decides, after a poll that reached the mailbox, whether to raise an alert, leave an
    /// existing one standing, or announce recovery. Kept separate from the poll loop so the
    /// ordering of the three cases is obvious.
    /// </summary>
    private async Task RaiseOrClearAlertsAsync(
        MailboxIngestConfig config,
        MailboxCredentials credentials,
        IReadOnlyList<string> realErrors,
        CancellationToken ct)
    {
        if (realErrors.Count > 0)
        {
            await _alerts.RaiseAsync(
                config, credentials, MailboxAlertKind.ReportFailed,
                string.Join("; ", realErrors.Take(3)), ct);
            return;
        }

        // Nothing failed — but a report that never arrives produces no error at all, which is
        // the failure mode most likely to go unnoticed. Treat an overdue report as a problem.
        var staleness = DescribeStaleness(config);
        if (staleness is not null)
        {
            await _alerts.RaiseAsync(config, credentials, MailboxAlertKind.NoReportReceived, staleness, ct);
            return;
        }

        await _alerts.ClearAsync(config, credentials, ct);
    }

    /// <summary>
    /// Returns a description of how overdue the next report is, or null when nothing is due.
    /// A config that has never ingested anything is measured from when it was created, so a
    /// brand new setup does not immediately alert.
    /// </summary>
    private static string? DescribeStaleness(MailboxIngestConfig config)
    {
        if (config.StaleAfterHours <= 0) return null;

        var since = config.LastReportIngestedAt ?? config.CreatedAt;
        var elapsed = DateTime.UtcNow - since;
        if (elapsed.TotalHours < config.StaleAfterHours) return null;

        var hours = (int)elapsed.TotalHours;
        return config.LastReportIngestedAt is null
            ? $"No report has been ingested since this mailbox was configured {hours} hours ago."
            : $"The last report was ingested {hours} hours ago, which is beyond the {config.StaleAfterHours} hour window.";
    }

    /// <summary>
    /// Works out who gets this property's summary, at send time rather than from a stored list.
    /// Deactivating a user or removing their property assignment therefore stops their mail on
    /// the very next report, with no list to remember to prune.
    /// </summary>
    private async Task<List<string>> ResolveRecipientsAsync(MailboxIngestConfig config, CancellationToken ct)
    {
        var fromUsers = await _db.UserProperties
            .Where(up => up.PropertyId == config.PropertyId
                && up.ReceivesSummary
                && up.User.IsActive
                && up.User.TenantId == config.Property.TenantId
                && up.User.Email != null)
            .Select(up => up.User.Email!)
            .ToListAsync(ct);

        // External recipients are for people who need the report but have no account —
        // a management company contact, say — so they are additive, not a fallback.
        var external = config.SummaryRecipients ?? Array.Empty<string>();

        var recipients = fromUsers
            .Concat(external)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Resolved {Total} summary recipient(s) for property {PropertyId}: {UserCount} from user accounts, {ExternalCount} external",
            recipients.Count, config.PropertyId, fromUsers.Count, external.Length);

        return recipients;
    }

    private static string BuildEmailBody(IngestionOutcome result)
    {
        var body = new StringBuilder();
        body.Append("<div style=\"font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:14px;line-height:1.6;color:#1f2937\">");

        var urgent = result.UrgentItems ?? Array.Empty<string>();
        if (urgent.Count > 0)
        {
            body.Append("<div style=\"border-left:4px solid #dc2626;background:#fef2f2;padding:12px 16px;margin-bottom:20px\">");
            body.Append("<strong style=\"color:#991b1b\">Urgent Items</strong><ul style=\"margin:8px 0 0;padding-left:20px\">");
            foreach (var item in urgent)
                body.Append("<li>").Append(System.Net.WebUtility.HtmlEncode(item)).Append("</li>");
            body.Append("</ul></div>");
        }

        // AiSummaryHtml is model-generated markup, not operator input; it is inserted as HTML
        // by design. The recipient list is operator-controlled.
        body.Append(result.AiSummaryHtml ?? "<p>No summary was generated for this report.</p>");

        body.Append("<hr style=\"border:0;border-top:1px solid #e5e7eb;margin:24px 0\">");
        body.Append("<p style=\"font-size:12px;color:#6b7280\">Generated automatically by SecurityRecap from the patrol report PDF. ");
        body.Append("The full summary is attached as a Markdown file.</p>");
        body.Append("</div>");

        return body.ToString();
    }

    private async Task ApplyPostProcessingAsync(
        MailboxIngestConfig config,
        MailboxCredentials credentials,
        MailboxMessage message,
        CancellationToken ct)
    {
        // Mailbox housekeeping is best-effort: the report is already ingested, and the
        // ExternalId guard means reprocessing the same message is a no-op.
        try
        {
            if (config.MarkAsRead)
                await _mailbox.MarkAsReadAsync(credentials, message.Id, ct);

            if (!string.IsNullOrWhiteSpace(config.MoveToFolder))
                await _mailbox.MoveAsync(credentials, message.Id, config.MoveToFolder, ct);
        }
        catch (MailboxException ex)
        {
            _logger.LogWarning(ex,
                "Could not apply mailbox housekeeping to message {MessageId}; the report was still ingested",
                message.Id);
        }
    }

    // ---------------------------------------------------------------- helpers

    private async Task<MailboxIngestConfig> LoadConfigAsync(Guid propertyId, CancellationToken ct)
    {
        return await _db.MailboxIngestConfigs
            .Include(c => c.Property)
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId, ct)
            ?? throw new KeyNotFoundException($"No mailbox configuration exists for property {propertyId}");
    }

    private MailboxCredentials ResolveCredentials(MailboxIngestConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.GraphClientSecretProtected))
            throw new MailboxException(MailboxFailureKind.Configuration,
                "No client secret is stored for this mailbox configuration.");

        var secret = _secrets.Unprotect(config.GraphClientSecretProtected);

        return new MailboxCredentials(
            config.GraphTenantId, config.GraphClientId, secret, config.MailboxAddress);
    }

    private static string DescribeCredentialFailure(Exception ex) => ex switch
    {
        CryptographicException =>
            "The stored client secret could not be decrypted. This happens when the Data Protection key ring "
            + "is lost — for example if DataProtection:KeyPath is unset and the keys were wiped by a deploy. "
            + "Re-enter the client secret to fix it.",
        _ => ex.Message
    };

    private DateTime ReceivedAfter(MailboxIngestConfig config)
    {
        var lookbackFloor = DateTime.UtcNow.AddDays(-Math.Max(1, config.LookbackDays));

        return config.LastMessageReceivedAt is { } last
            ? Later(last - WatermarkOverlap, lookbackFloor)
            : lookbackFloor;
    }

    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime? Max(DateTime? current, DateTime candidate) =>
        current is null || candidate > current.Value ? candidate : current;

    private async Task<MailboxPollResult> FailAsync(
        MailboxIngestConfig config,
        string propertyName,
        string error,
        IReadOnlyList<MailboxMessageOutcome> outcomes,
        MailboxCredentials? credentials,
        CancellationToken ct)
    {
        config.LastError = error;
        config.ConsecutiveFailures++;

        // Alerting needs the same Graph credentials that just failed. When the credentials
        // themselves are the problem there is nothing to send with, so the failure is only
        // logged — the staleness check will catch it once a report goes missing.
        if (credentials is not null)
            await _alerts.RaiseAsync(config, credentials, MailboxAlertKind.PollFailed, error, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogError("Mailbox poll for {PropertyName} failed (attempt {Attempt}): {Error}",
            propertyName, config.ConsecutiveFailures, error);

        return new MailboxPollResult(config.PropertyId, propertyName, 0, 0, 0, outcomes, error);
    }
}
