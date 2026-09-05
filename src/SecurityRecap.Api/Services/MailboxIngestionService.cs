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
    private readonly ILogger<MailboxIngestionService> _logger;

    public MailboxIngestionService(
        AppDbContext db,
        IMailboxClient mailbox,
        IIngestionService ingestion,
        ISecretProtector secrets,
        ILogger<MailboxIngestionService> logger)
    {
        _db = db;
        _mailbox = mailbox;
        _ingestion = ingestion;
        _secrets = secrets;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> GetPropertiesDueForPollAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var candidates = await _db.MailboxIngestConfigs
            .Where(c => c.IsEnabled && c.Property.IsActive)
            .Select(c => new { c.PropertyId, c.LastPolledAt, c.PollIntervalMinutes, c.ConsecutiveFailures })
            .ToListAsync(ct);

        return candidates
            .Where(c => c.LastPolledAt is null || now >= c.LastPolledAt.Value.AddMinutes(NextDelay(c.PollIntervalMinutes, c.ConsecutiveFailures)))
            .Select(c => c.PropertyId)
            .ToList();
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
            return await FailAsync(config, propertyName, DescribeCredentialFailure(ex), outcomes, ct);
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
            return await FailAsync(config, propertyName, ex.Message, outcomes, ct);
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

            IReadOnlyList<MailboxAttachment> pdfs;
            try
            {
                pdfs = await _mailbox.FetchPdfAttachmentsAsync(
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
        MailboxAttachment pdf,
        MailboxCredentials credentials,
        CancellationToken ct)
    {
        // A message can carry more than one report PDF, so the idempotency key has to be
        // per attachment rather than per message.
        var externalId = $"graph:{message.InternetMessageId}:{pdf.Name}";

        try
        {
            await using var stream = new MemoryStream(pdf.Content);

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
        var recipients = config.SummaryRecipients
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Summary email is enabled for property {PropertyId} but no recipients are configured",
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
        CancellationToken ct)
    {
        config.LastError = error;
        config.ConsecutiveFailures++;
        await _db.SaveChangesAsync(ct);

        _logger.LogError("Mailbox poll for {PropertyName} failed (attempt {Attempt}): {Error}",
            propertyName, config.ConsecutiveFailures, error);

        return new MailboxPollResult(config.PropertyId, propertyName, 0, 0, 0, outcomes, error);
    }
}
