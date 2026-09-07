using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Exceptions;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Api.Controllers;

[ApiController]
[Route("api/v1/mailbox-ingest")]
[Authorize(Policy = "AdminOnly")]
public class MailboxIngestController : BaseApiController
{
    private readonly AppDbContext _db;
    private readonly IMailboxIngestionService _mailboxIngestion;
    private readonly ISecretProtector _secrets;

    public MailboxIngestController(
        AppDbContext db,
        IMailboxIngestionService mailboxIngestion,
        ISecretProtector secrets)
    {
        _db = db;
        _mailboxIngestion = mailboxIngestion;
        _secrets = secrets;
    }

    [HttpGet("{propertyId:guid}")]
    public async Task<ActionResult<ApiResponse<MailboxIngestConfigDto?>>> Get(Guid propertyId)
    {
        if (!await CanAccessPropertyAsync(propertyId))
            return NotFound(ApiResponse<MailboxIngestConfigDto?>.Fail("Property not found for tenant"));

        var config = await _db.MailboxIngestConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId);

        // No configuration yet is a normal state, not an error — the UI renders an empty form.
        return Ok(ApiResponse<MailboxIngestConfigDto?>.Ok(config is null ? null : ToDto(config)));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<MailboxIngestConfigDto>>> Save(
        [FromBody] SaveMailboxIngestConfigRequest request)
    {
        if (!await CanAccessPropertyAsync(request.PropertyId))
            return NotFound(ApiResponse<MailboxIngestConfigDto>.Fail("Property not found for tenant"));

        var invalidRecipient = request.SummaryRecipients
            .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r) && !IsEmailLike(r));
        if (invalidRecipient is not null)
            return BadRequest(ApiResponse<MailboxIngestConfigDto>.Fail(
                $"'{invalidRecipient}' is not a valid email address"));

        var recipients = request.SummaryRecipients
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Recipients can now come from user accounts flagged for this property, so an empty
        // external list is fine — but both being empty means the mail would go nowhere.
        if (request.SendSummaryEmail && recipients.Length == 0)
        {
            var flaggedUsers = await _db.UserProperties
                .CountAsync(up => up.PropertyId == request.PropertyId
                    && up.ReceivesSummary
                    && up.User.IsActive);

            if (flaggedUsers == 0)
                return BadRequest(ApiResponse<MailboxIngestConfigDto>.Fail(
                    "No one would receive the summary. Flag a user to receive it for this property, "
                    + "or add an external recipient."));
        }

        if (!string.IsNullOrWhiteSpace(request.ScheduleTimeZone))
        {
            try { TimeZoneInfo.FindSystemTimeZoneById(request.ScheduleTimeZone.Trim()); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return BadRequest(ApiResponse<MailboxIngestConfigDto>.Fail(
                    $"'{request.ScheduleTimeZone}' is not a recognised timezone. Use an IANA id such as America/New_York."));
            }
        }

        if (request.ActiveWindowStart is null != request.ActiveWindowEnd is null)
            return BadRequest(ApiResponse<MailboxIngestConfigDto>.Fail(
                "Set both a window start and end, or neither."));

        var config = await _db.MailboxIngestConfigs
            .FirstOrDefaultAsync(c => c.PropertyId == request.PropertyId);

        if (config is null && string.IsNullOrWhiteSpace(request.GraphClientSecret))
            return BadRequest(ApiResponse<MailboxIngestConfigDto>.Fail(
                "A client secret is required when first configuring a mailbox"));

        if (config is null)
        {
            config = new MailboxIngestConfig { Id = Guid.NewGuid(), PropertyId = request.PropertyId };
            _db.MailboxIngestConfigs.Add(config);
        }

        config.GraphTenantId = request.GraphTenantId.Trim();
        config.GraphClientId = request.GraphClientId.Trim();
        config.MailboxAddress = request.MailboxAddress.Trim();
        config.FolderName = string.IsNullOrWhiteSpace(request.FolderName) ? "inbox" : request.FolderName.Trim();
        config.FromAddress = Blank(request.FromAddress);
        config.SubjectContains = Blank(request.SubjectContains);
        config.AttachmentNameContains = Blank(request.AttachmentNameContains);
        config.LookbackDays = request.LookbackDays;
        config.PollIntervalMinutes = request.PollIntervalMinutes;
        config.StaleAfterHours = request.StaleAfterHours;
        config.ActiveWindowStart = request.ActiveWindowStart;
        config.ActiveWindowEnd = request.ActiveWindowEnd;
        config.ActiveWindowPollMinutes = request.ActiveWindowPollMinutes;
        config.ScheduleTimeZone = Blank(request.ScheduleTimeZone);
        config.MarkAsRead = request.MarkAsRead;
        config.MoveToFolder = Blank(request.MoveToFolder);
        config.SendSummaryEmail = request.SendSummaryEmail;
        config.SummaryRecipients = recipients;
        config.SummarySubjectPrefix = Blank(request.SummarySubjectPrefix);
        config.IsEnabled = request.IsEnabled;

        // An empty secret means "keep what is stored"; the DTO never returns the real value,
        // so a plain save from the edit form must not wipe it.
        if (!string.IsNullOrWhiteSpace(request.GraphClientSecret))
        {
            config.GraphClientSecretProtected = _secrets.Protect(request.GraphClientSecret.Trim());
            // New credentials deserve a clean slate on the failure backoff.
            config.ConsecutiveFailures = 0;
            config.LastError = null;
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<MailboxIngestConfigDto>.Ok(ToDto(config)));
    }

    [HttpDelete("{propertyId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid propertyId)
    {
        if (!await CanAccessPropertyAsync(propertyId))
            return NotFound(ApiResponse<bool>.Fail("Property not found for tenant"));

        var config = await _db.MailboxIngestConfigs.FirstOrDefaultAsync(c => c.PropertyId == propertyId);
        if (config is null) return Ok(ApiResponse<bool>.Ok(false));

        _db.MailboxIngestConfigs.Remove(config);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("{propertyId:guid}/verify")]
    public async Task<ActionResult<ApiResponse<MailboxVerifyResultDto>>> Verify(Guid propertyId)
    {
        if (!await CanAccessPropertyAsync(propertyId))
            return NotFound(ApiResponse<MailboxVerifyResultDto>.Fail("Property not found for tenant"));

        try
        {
            var mailbox = await _mailboxIngestion.VerifyConfigurationAsync(propertyId, HttpContext.RequestAborted);
            return Ok(ApiResponse<MailboxVerifyResultDto>.Ok(new MailboxVerifyResultDto(mailbox)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MailboxVerifyResultDto>.Fail(ex.Message));
        }
        catch (MailboxException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse<MailboxVerifyResultDto>.Fail(ex.Message));
        }
    }

    /// <summary>Runs a poll immediately instead of waiting for the next scheduled one.</summary>
    [HttpPost("{propertyId:guid}/poll")]
    public async Task<ActionResult<ApiResponse<MailboxPollResultDto>>> Poll(Guid propertyId)
    {
        if (!await CanAccessPropertyAsync(propertyId))
            return NotFound(ApiResponse<MailboxPollResultDto>.Fail("Property not found for tenant"));

        try
        {
            var result = await _mailboxIngestion.PollPropertyAsync(propertyId, HttpContext.RequestAborted);
            return Ok(ApiResponse<MailboxPollResultDto>.Ok(ToDto(result)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MailboxPollResultDto>.Fail(ex.Message));
        }
    }

    // ---------------------------------------------------------------- helpers

    private async Task<bool> CanAccessPropertyAsync(Guid propertyId)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var userRole = GetUserRole();

        return await _db.Properties
            .ApplyPropertyAccess(tenantId, userId, userRole)
            .AnyAsync(p => p.Id == propertyId);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsEmailLike(string value)
    {
        var trimmed = value.Trim();
        var at = trimmed.IndexOf('@');
        return at > 0
            && at < trimmed.Length - 1
            && trimmed.IndexOf('@', at + 1) < 0
            && !trimmed.Any(char.IsWhiteSpace)
            && trimmed.LastIndexOf('.') > at + 1;
    }

    private static MailboxIngestConfigDto ToDto(MailboxIngestConfig c) => new(
        c.PropertyId,
        c.GraphTenantId,
        c.GraphClientId,
        !string.IsNullOrWhiteSpace(c.GraphClientSecretProtected),
        c.MailboxAddress,
        c.FolderName,
        c.FromAddress,
        c.SubjectContains,
        c.AttachmentNameContains,
        c.LookbackDays,
        c.PollIntervalMinutes,
        c.ActiveWindowStart,
        c.ActiveWindowEnd,
        c.ActiveWindowPollMinutes,
        c.ScheduleTimeZone,
        c.StaleAfterHours,
        c.MarkAsRead,
        c.MoveToFolder,
        c.SendSummaryEmail,
        c.SummaryRecipients,
        c.SummarySubjectPrefix,
        c.IsEnabled,
        c.LastPolledAt,
        c.LastSuccessAt,
        c.LastMessageReceivedAt,
        c.LastError,
        c.ConsecutiveFailures,
        c.LastReportIngestedAt,
        c.LastAlertAt);

    private static MailboxPollResultDto ToDto(MailboxPollResult r) => new(
        r.PropertyId,
        r.PropertyName,
        r.Succeeded,
        r.MessagesExamined,
        r.ReportsIngested,
        r.MessagesSkipped,
        r.Messages.Select(m => new MailboxMessageOutcomeDto(
            m.Subject, m.FromAddress, m.ReceivedAtUtc, m.FileName,
            m.ReportId, m.AlreadyIngested, m.SummaryEmailSent, m.Error)).ToList(),
        r.Error);
}
