using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Exceptions;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Api.Services;

public enum MailboxAlertKind
{
    /// <summary>The mailbox itself could not be reached or read.</summary>
    PollFailed,

    /// <summary>The mailbox was fine, but a report could not be processed.</summary>
    ReportFailed,

    /// <summary>No report has been ingested for longer than the configured window.</summary>
    NoReportReceived
}

/// <summary>
/// Emails the operator when report pickup breaks, so a silent failure does not go unnoticed
/// until someone wonders where the summary went. Deliberately noisy about being quiet: it
/// also reports when a report simply never arrived, which no error would otherwise surface.
/// </summary>
public class MailboxAlertNotifier
{
    /// <summary>
    /// How long before the same unresolved problem is mailed again. Long enough that a
    /// persistent failure does not flood the inbox, short enough to be a daily nag.
    /// </summary>
    private static readonly TimeSpan RepeatAfter = TimeSpan.FromHours(12);

    private readonly AppDbContext _db;
    private readonly IMailboxClient _mailbox;
    private readonly ILogger<MailboxAlertNotifier> _logger;

    public MailboxAlertNotifier(AppDbContext db, IMailboxClient mailbox, ILogger<MailboxAlertNotifier> logger)
    {
        _db = db;
        _mailbox = mailbox;
        _logger = logger;
    }

    /// <summary>
    /// Raises an alert unless the same problem was already reported recently. Mutates the
    /// config's alert bookkeeping; the caller is responsible for saving.
    /// </summary>
    public async Task RaiseAsync(
        MailboxIngestConfig config,
        MailboxCredentials credentials,
        MailboxAlertKind kind,
        string detail,
        CancellationToken ct = default)
    {
        var signature = $"{kind}:{Fingerprint(detail)}";
        var now = DateTime.UtcNow;

        var alreadyReported = config.LastAlertSignature == signature
            && config.LastAlertAt is { } last
            && now - last < RepeatAfter;

        if (alreadyReported)
        {
            _logger.LogDebug(
                "Suppressing repeat {Kind} alert for property {PropertyId}; last sent {LastAlertAt}",
                kind, config.PropertyId, config.LastAlertAt);
            return;
        }

        var recipients = await ResolveAlertRecipientsAsync(config, ct);
        if (recipients.Count == 0)
        {
            // Record the state anyway so a later recovery notice is still coherent.
            config.LastAlertSignature = signature;
            config.LastAlertAt = now;
            _logger.LogWarning(
                "Report pickup problem for property {PropertyId} ({Kind}) but nobody is flagged to receive alerts: {Detail}",
                config.PropertyId, kind, detail);
            return;
        }

        var subject = $"[SecurityRecap] {Headline(kind)} - {config.Property.Name}";

        try
        {
            await _mailbox.SendMailAsync(
                credentials, recipients, subject, BuildBody(config, kind, detail), null, ct);

            config.LastAlertSignature = signature;
            config.LastAlertAt = now;

            _logger.LogInformation(
                "Sent {Kind} alert for property {PropertyId} to {Count} recipient(s)",
                kind, config.PropertyId, recipients.Count);
        }
        catch (MailboxException ex)
        {
            // If Graph is the thing that is broken, the alert cannot get out. Log loudly and
            // leave the signature unset so the alert is retried rather than considered sent.
            _logger.LogError(ex,
                "Could not send {Kind} alert for property {PropertyId}. Original problem: {Detail}",
                kind, config.PropertyId, detail);
        }
    }

    /// <summary>
    /// Sends a recovery notice if the property was previously in an alerted state, and clears
    /// that state. No-op when nothing was wrong.
    /// </summary>
    public async Task ClearAsync(
        MailboxIngestConfig config, MailboxCredentials credentials, CancellationToken ct = default)
    {
        if (config.LastAlertSignature is null) return;

        var wasReporting = config.LastAlertSignature;
        config.LastAlertSignature = null;
        config.LastAlertAt = null;

        var recipients = await ResolveAlertRecipientsAsync(config, ct);
        if (recipients.Count == 0) return;

        var body = new StringBuilder();
        Open(body);
        body.Append("<p>Report pickup for <strong>")
            .Append(Encode(config.Property.Name))
            .Append("</strong> is working again. The most recent check completed without errors.</p>");
        AppendStatus(body, config);
        Close(body);

        try
        {
            await _mailbox.SendMailAsync(
                credentials, recipients,
                $"[SecurityRecap] Report pickup recovered - {config.Property.Name}",
                body.ToString(), null, ct);

            _logger.LogInformation(
                "Sent recovery notice for property {PropertyId} (was: {Signature})",
                config.PropertyId, wasReporting);
        }
        catch (MailboxException ex)
        {
            _logger.LogWarning(ex, "Could not send recovery notice for property {PropertyId}", config.PropertyId);
        }
    }

    /// <summary>
    /// Alerts go to users flagged for them, resolved live — the same principle as summary
    /// recipients, so deactivating someone stops their alerts too.
    /// </summary>
    private async Task<List<string>> ResolveAlertRecipientsAsync(MailboxIngestConfig config, CancellationToken ct)
    {
        return await _db.UserProperties
            .Where(up => up.PropertyId == config.PropertyId
                && up.ReceivesAlerts
                && up.User.IsActive
                && up.User.TenantId == config.Property.TenantId
                && up.User.Email != null)
            .Select(up => up.User.Email!)
            .Distinct()
            .ToListAsync(ct);
    }

    private static string Headline(MailboxAlertKind kind) => kind switch
    {
        MailboxAlertKind.PollFailed => "Report pickup is failing",
        MailboxAlertKind.ReportFailed => "A report could not be processed",
        MailboxAlertKind.NoReportReceived => "No report has arrived",
        _ => "Report pickup problem"
    };

    private static string BuildBody(MailboxIngestConfig config, MailboxAlertKind kind, string detail)
    {
        var body = new StringBuilder();
        Open(body);

        body.Append("<p><strong>").Append(Encode(Headline(kind))).Append("</strong> for ")
            .Append(Encode(config.Property.Name)).Append(".</p>");

        body.Append("<div style=\"border-left:4px solid #dc2626;background:#fef2f2;padding:12px 16px;margin:16px 0\">")
            .Append(Encode(detail))
            .Append("</div>");

        body.Append("<p>").Append(Encode(WhatToCheck(kind))).Append("</p>");

        AppendStatus(body, config);

        body.Append("<p style=\"font-size:12px;color:#6b7280\">Automatic checks continue in the background; ")
            .Append("this alert repeats at most twice a day while the problem persists, and you will get a ")
            .Append("notice when it clears.</p>");

        Close(body);
        return body.ToString();
    }

    private static string WhatToCheck(MailboxAlertKind kind) => kind switch
    {
        MailboxAlertKind.PollFailed =>
            "This usually means the Microsoft Graph credentials have expired or permissions changed. "
            + "Open Settings, Automated Report Pickup, and use Test connection to confirm.",
        MailboxAlertKind.ReportFailed =>
            "The mailbox was reachable, so this is about the report itself: the PDF may be malformed, "
            + "or the analysis step may have failed. The message stays unprocessed and will be retried "
            + "on the next check.",
        MailboxAlertKind.NoReportReceived =>
            "Nothing has failed - a report simply has not arrived. Check the security company is still "
            + "sending to this mailbox, and that nothing is filtering it out of the inbox.",
        _ => "Open Settings, Automated Report Pickup, to review."
    };

    private static void AppendStatus(StringBuilder body, MailboxIngestConfig config)
    {
        body.Append("<table style=\"font-size:13px;border-collapse:collapse;margin-top:8px\">");
        Row(body, "Mailbox", config.MailboxAddress);
        Row(body, "Last checked", Format(config.LastPolledAt));
        Row(body, "Last successful check", Format(config.LastSuccessAt));
        Row(body, "Last report ingested", Format(config.LastReportIngestedAt));
        if (config.ConsecutiveFailures > 0)
            Row(body, "Consecutive failures", config.ConsecutiveFailures.ToString());
        body.Append("</table>");
    }

    private static void Row(StringBuilder body, string label, string value)
    {
        body.Append("<tr><td style=\"padding:2px 16px 2px 0;color:#6b7280\">")
            .Append(Encode(label))
            .Append("</td><td style=\"padding:2px 0\">")
            .Append(Encode(value))
            .Append("</td></tr>");
    }

    private static string Format(DateTime? value) =>
        value is null ? "Never" : value.Value.ToString("yyyy-MM-dd HH:mm 'UTC'");

    private static void Open(StringBuilder body) =>
        body.Append("<div style=\"font-family:Segoe UI,Helvetica,Arial,sans-serif;font-size:14px;line-height:1.6;color:#1f2937\">");

    private static void Close(StringBuilder body) => body.Append("</div>");

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Collapses an error to something stable enough to compare across polls. Graph and Claude
    /// errors carry request ids and timestamps that would otherwise make every occurrence look
    /// like a new problem and defeat the throttle.
    /// </summary>
    private static string Fingerprint(string detail)
    {
        var trimmed = new string(detail
            .Where(c => !char.IsDigit(c))
            .ToArray())
            .Trim();

        return trimmed.Length <= 120 ? trimmed : trimmed[..120];
    }
}
