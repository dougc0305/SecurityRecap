using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Api.Prompts;
using SecurityRecap.Core;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Exceptions;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Api.Services;

public class RecapService : IRecapService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppDbContext _db;
    private readonly IClaudeApiService _claudeApi;
    private readonly ILogger<RecapService> _logger;

    public RecapService(AppDbContext db, IClaudeApiService claudeApi, ILogger<RecapService> logger)
    {
        _db = db;
        _claudeApi = claudeApi;
        _logger = logger;
    }

    public async Task<RecapResult> BuildAsync(
        Guid tenantId, Guid userId, UserRole userRole,
        Guid propertyId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var data = await BuildDataAsync(tenantId, userId, userRole, propertyId, from, to, ct);

        if (data.Coverage.UsableReports == 0)
        {
            return new RecapResult(data, null,
                "No usable reports in this period, so there is nothing to summarise.");
        }

        try
        {
            var prompt = RecapPrompt.Build(JsonSerializer.Serialize(data, JsonOptions));
            var response = await _claudeApi.ChatAsync(prompt, new[]
            {
                new ChatMessage("user",
                    "Produce the recap for this period using only the figures supplied.")
            });

            var narrative = JsonSerializer.Deserialize<RecapNarrative>(StripCodeFences(response), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            });

            return new RecapResult(data, narrative, null);
        }
        catch (ClaudeApiException ex)
        {
            // The figures are the part a board cannot do without, so they are still returned.
            _logger.LogError(ex, "Recap narrative failed for property {PropertyId}", propertyId);
            return new RecapResult(data, null, ClaudeFailureFormatter.ToUserMessage(ex));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Recap narrative was not valid JSON for property {PropertyId}", propertyId);
            return new RecapResult(data, null,
                "The summary could not be read back. The figures below are unaffected.");
        }
    }

    public async Task<RecapData> BuildDataAsync(
        Guid tenantId, Guid userId, UserRole userRole,
        Guid propertyId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var property = await _db.Properties
            .ApplyPropertyAccess(tenantId, userId, userRole)
            .Where(p => p.Id == propertyId)
            .Select(p => new { p.Name, p.Timezone })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Property {propertyId} not found for tenant");

        var tz = ResolveTimeZone(property.Timezone);
        DateTime? Local(DateTime? utc) => utc is null
            ? null
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), tz);

        var reports = await _db.Reports
            .Where(r => r.PropertyId == propertyId && r.ReportDate >= from && r.ReportDate <= to)
            .Select(r => new { r.Id, r.ReportDate, r.OfficerNames })
            .ToListAsync(ct);

        var reportIds = reports.Select(r => r.Id).ToList();

        var incidents = await _db.Incidents
            .Where(i => reportIds.Contains(i.ReportId))
            .Select(i => new
            {
                i.Id,
                i.ReportId,
                i.IncidentTime,
                i.CreatedAt,
                IncidentType = i.IncidentType.ToString(),
                Severity = i.Severity.ToString(),
                i.Location,
                i.Description
            })
            .ToListAsync(ct);

        var reportDateById = reports.ToDictionary(r => r.Id, r => r.ReportDate);

        // ---- coverage -------------------------------------------------------
        var nights = to.DayNumber - from.DayNumber + 1;
        var received = reports.Select(r => r.ReportDate).ToHashSet();
        var missing = Enumerable.Range(0, nights)
            .Select(offset => from.AddDays(offset))
            .Where(d => !received.Contains(d))
            .ToList();

        var entriesPerReport = reports.ToDictionary(
            r => r.Id, r => incidents.Count(i => i.ReportId == r.Id));

        // A report that stored no entries at all is a processing failure, not a quiet night:
        // it must not be counted as coverage or it silently understates the period.
        var emptyReportDates = reports
            .Where(r => entriesPerReport[r.Id] == 0)
            .Select(r => r.ReportDate)
            .OrderBy(d => d)
            .ToList();

        var usableReportIds = reports.Where(r => entriesPerReport[r.Id] > 0).Select(r => r.Id).ToHashSet();

        var patrolCounts = reports
            .Where(r => usableReportIds.Contains(r.Id))
            .Select(r => incidents.Count(i => i.ReportId == r.Id && i.IncidentType == "Patrol"))
            .ToList();

        var officers = reports
            .Where(r => usableReportIds.Contains(r.Id))
            .SelectMany(r => r.OfficerNames)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .GroupBy(NormalizeOfficer, StringComparer.OrdinalIgnoreCase)
            .Select(g => new OfficerShifts(g.First(), g.Count()))
            .OrderByDescending(o => o.Shifts)
            .ToList();

        var coverage = new RecapCoverage(
            from, to, nights, reports.Count, usableReportIds.Count,
            missing, emptyReportDates, officers,
            patrolCounts.Count > 0 ? Math.Round(patrolCounts.Average(), 1) : 0,
            patrolCounts.Count > 0 ? patrolCounts.Min() : 0,
            patrolCounts.Count > 0 ? patrolCounts.Max() : 0);

        // ---- entries --------------------------------------------------------
        // Routine patrol rounds and scheduled gate locks are logged whether or not anything
        // happened. Counting them as "incidents" inflates the period by an order of magnitude.
        bool IsRoutine(string type, string severity) =>
            severity == "Low" && (type == "Patrol" || type == "Gate");

        var substantive = incidents.Where(i => !IsRoutine(i.IncidentType, i.Severity)).ToList();

        var byCategory = substantive
            .GroupBy(i => i.IncidentType)
            .Select(g => new RecapCategoryCount(
                g.Key,
                g.Count(x => x.Severity == "High" || x.Severity == "Urgent"),
                g.Count(x => x.Severity == "Medium"),
                g.Count(x => x.Severity == "Low"),
                g.Count()))
            .OrderByDescending(c => c.Total)
            .ToList();

        RecapEntry ToEntry(dynamic i) => new(
            reportDateById[(Guid)i.ReportId],
            Local((DateTime?)i.IncidentTime ?? (DateTime)i.CreatedAt),
            (string)i.IncidentType, (string)i.Severity, (string?)i.Location, (string)i.Description);

        var substantiveDetail = substantive
            .OrderBy(i => i.IncidentTime ?? i.CreatedAt)
            .Select(i => ToEntry(i))
            .ToList();

        var highSeverity = substantive
            .Where(i => i.Severity is "High" or "Urgent")
            .OrderBy(i => i.IncidentTime ?? i.CreatedAt)
            .Select(i => ToEntry(i))
            .ToList();

        // ---- violations -----------------------------------------------------
        var incidentIds = incidents.Select(i => i.Id).ToList();
        var violationRows = await _db.Violations
            .Where(v => incidentIds.Contains(v.IncidentId))
            .Select(v => new
            {
                v.IncidentId,
                v.ViolationType,
                v.Location,
                v.NoticeIssued,
                v.TowNotified,
                Plate = v.Vehicle != null ? v.Vehicle.PlateNumber : null,
                State = v.Vehicle != null ? v.Vehicle.PlateState : null,
                Make = v.Vehicle != null ? v.Vehicle.Make : null,
                Model = v.Vehicle != null ? v.Vehicle.Model : null,
                Color = v.Vehicle != null ? v.Vehicle.Color : null,
                AllTime = v.Vehicle != null ? v.Vehicle.ViolationCount : 0,
                FirstSeen = v.Vehicle != null ? v.Vehicle.FirstSeen : null
            })
            .ToListAsync(ct);

        var platesInPeriod = violationRows
            .Where(v => PlateNumber.IsTrackable(v.Plate))
            .GroupBy(v => v.Plate!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var incidentById = incidents.ToDictionary(i => i.Id);

        var violations = violationRows
            .Select(v =>
            {
                var inc = incidentById[v.IncidentId];
                var vehicle = string.Join(' ', new[] { v.Color, v.Make, v.Model }
                    .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

                return new RecapViolation(
                    reportDateById[inc.ReportId],
                    Local(inc.IncidentTime ?? inc.CreatedAt),
                    PlateNumber.IsTrackable(v.Plate) ? v.Plate : null,
                    v.State,
                    string.IsNullOrWhiteSpace(vehicle) ? null : vehicle,
                    v.Location ?? inc.Location,
                    v.ViolationType,
                    inc.Severity,
                    v.NoticeIssued,
                    v.TowNotified,
                    v.AllTime,
                    Local(v.FirstSeen),
                    PlateNumber.IsTrackable(v.Plate) ? platesInPeriod[v.Plate!] : 0);
            })
            .OrderBy(v => v.LocalTime)
            .ToList();

        // ---- recurrence -----------------------------------------------------
        // The same problem reported night after night is the finding, not the raw count.
        var recurringMaintenance = substantive
            .Where(i => i.IncidentType == "Maintenance")
            .GroupBy(i => NormalizeIssue(i.Description, i.Location), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new RecapRecurringItem(
                g.First().Description,
                g.First().Location,
                g.Count(),
                g.Min(x => reportDateById[x.ReportId]),
                g.Max(x => reportDateById[x.ReportId])))
            .OrderByDescending(r => r.Occurrences)
            .ToList();

        var repeatLocations = substantive
            .Where(i => !string.IsNullOrWhiteSpace(i.Location))
            .GroupBy(i => i.Location!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => new RecapRecurringItem(
                string.Join(", ", g.Select(x => x.IncidentType).Distinct()),
                g.Key,
                g.Count(),
                g.Min(x => reportDateById[x.ReportId]),
                g.Max(x => reportDateById[x.ReportId])))
            .OrderByDescending(r => r.Occurrences)
            .ToList();

        return new RecapData(
            property.Name,
            property.Timezone,
            coverage,
            incidents.Count,
            incidents.Count - substantive.Count,
            substantive.Count,
            byCategory,
            substantiveDetail,
            highSeverity,
            violations,
            recurringMaintenance,
            repeatLocations);
    }

    /// <summary>
    /// Collapses "Sprinkler head broken on right side of driveway" logged nightly at one
    /// address into a single recurring item, without merging genuinely different problems.
    /// </summary>
    private static string NormalizeIssue(string description, string? location)
    {
        var text = description.ToLowerInvariant();
        var cut = text.IndexOf(". photo", StringComparison.Ordinal);
        if (cut > 0) text = text[..cut];
        return $"{location?.Trim().ToLowerInvariant()}|{text.Trim()}";
    }

    /// <summary>
    /// Officers appear under slight spelling variants between shifts ("Garcia-Davila" and
    /// "GarciaDavila"), which would otherwise show as separate people in a board document.
    /// </summary>
    private static string NormalizeOfficer(string name) =>
        new string(name.Where(char.IsLetter).ToArray()).ToLowerInvariant();

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string StripCodeFences(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            text = firstNewline >= 0 ? text[(firstNewline + 1)..] : string.Empty;
        }
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }
}
