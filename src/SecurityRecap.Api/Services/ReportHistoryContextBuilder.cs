using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Api.Services;

/// <summary>
/// Assembles the historical picture for a property that gets handed to Claude alongside the
/// PDF, so the generated summary can say "third noise complaint at this address this month"
/// rather than describing the shift in isolation.
/// </summary>
public class ReportHistoryContextBuilder
{
    private readonly AppDbContext _db;

    public ReportHistoryContextBuilder(AppDbContext db)
    {
        _db = db;
    }

    public record IncidentSnapshot(
        string? IncidentTimeLocal,
        string IncidentType,
        string Severity,
        string? Location,
        string Description);

    public record TypeCount(string IncidentType, int Last7Days, int Last30Days, int Last90Days);

    public record RepeatAddress(string Address, int IncidentCount, string? LastIncidentLocal, int Last30Days);

    public record KnownVehicle(
        string PlateNumber,
        string? PlateState,
        string? Make,
        string? Model,
        string? Color,
        int PriorViolationCount,
        string? FirstSeenLocal,
        string? LastSeenLocal);

    public record PropertyHistoryContext(
        string PropertyName,
        /// <summary>
        /// The timezone every timestamp below is expressed in. Patrol reports print local
        /// times, so the history must too — comparing a local time in the PDF against a UTC
        /// timestamp here manufactures an offset-sized "anomaly" out of nothing.
        /// </summary>
        string TimeZone,
        string GeneratedAtLocal,
        int ReportsOnFile,
        DateOnly? EarliestReportDate,
        DateOnly? LatestReportDate,
        int TotalIncidents90Days,
        double AverageIncidentsPerReport30Days,
        int? DaysSinceLastHighOrUrgent,
        string? LastHighOrUrgentDescription,
        IReadOnlyList<TypeCount> IncidentCountsByType,
        IReadOnlyList<RepeatAddress> RepeatAddresses,
        IReadOnlyList<KnownVehicle> KnownVehicles,
        IReadOnlyList<IncidentSnapshot> RecentIncidents);

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public async Task<PropertyHistoryContext> BuildAsync(Guid propertyId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since7 = now.AddDays(-7);
        var since30 = now.AddDays(-30);
        var since90 = now.AddDays(-90);

        var property = await _db.Properties
            .Where(p => p.Id == propertyId)
            .Select(p => new { p.Name, p.Timezone })
            .FirstOrDefaultAsync(ct);

        var propertyName = property?.Name ?? "Unknown property";
        var timeZoneId = string.IsNullOrWhiteSpace(property?.Timezone) ? "UTC" : property!.Timezone;
        var timeZone = ResolveTimeZone(timeZoneId);

        // Everything handed to the model is rendered in the property's local time, because
        // that is what the patrol report itself prints.
        string? Local(DateTime? utc) => utc is null
            ? null
            : TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), timeZone).ToString("yyyy-MM-dd HH:mm");

        // Incidents are timestamped by IncidentTime when the report supplied one, and by
        // CreatedAt otherwise; coalesce so undated incidents still land in a window.
        var incidents90 = await _db.Incidents
            .Where(i => i.PropertyId == propertyId && (i.IncidentTime ?? i.CreatedAt) >= since90)
            .Select(i => new
            {
                i.IncidentTime,
                i.CreatedAt,
                IncidentType = i.IncidentType.ToString(),
                Severity = i.Severity.ToString(),
                i.Location,
                i.Description
            })
            .ToListAsync(ct);

        DateTime Effective(DateTime? incidentTime, DateTime createdAt) => incidentTime ?? createdAt;

        var countsByType = incidents90
            .GroupBy(i => i.IncidentType)
            .Select(g => new TypeCount(
                g.Key,
                g.Count(i => Effective(i.IncidentTime, i.CreatedAt) >= since7),
                g.Count(i => Effective(i.IncidentTime, i.CreatedAt) >= since30),
                g.Count()))
            .OrderByDescending(t => t.Last90Days)
            .ToList();

        var reportStats = await _db.Reports
            .Where(r => r.PropertyId == propertyId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Earliest = g.Min(r => r.ReportDate),
                Latest = g.Max(r => r.ReportDate)
            })
            .FirstOrDefaultAsync(ct);

        var reportsLast30 = await _db.Reports
            .CountAsync(r => r.PropertyId == propertyId && r.CreatedAt >= since30, ct);

        var incidentsLast30 = incidents90.Count(i => Effective(i.IncidentTime, i.CreatedAt) >= since30);
        var averagePerReport = reportsLast30 > 0
            ? Math.Round((double)incidentsLast30 / reportsLast30, 2)
            : 0;

        var lastSerious = incidents90
            .Where(i => i.Severity is "High" or "Urgent")
            .OrderByDescending(i => Effective(i.IncidentTime, i.CreatedAt))
            .FirstOrDefault();

        int? daysSinceSerious = lastSerious is null
            ? null
            : (int)Math.Floor((now - Effective(lastSerious.IncidentTime, lastSerious.CreatedAt)).TotalDays);

        var repeatAddresses = await _db.AddressesOfInterest
            .Where(a => a.PropertyId == propertyId && a.IncidentCount > 1)
            .OrderByDescending(a => a.IncidentCount)
            .Take(15)
            .Select(a => new { a.Address, a.IncidentCount, a.LastIncident })
            .ToListAsync(ct);

        var repeatAddressList = repeatAddresses
            .Select(a => new RepeatAddress(
                a.Address,
                a.IncidentCount,
                Local(a.LastIncident),
                incidents90.Count(i =>
                    i.Location != null
                    && string.Equals(i.Location.Trim(), a.Address, StringComparison.OrdinalIgnoreCase)
                    && Effective(i.IncidentTime, i.CreatedAt) >= since30)))
            .ToList();

        // Every plate already on file, not just repeat offenders. A plate seen once before is
        // exactly the case worth flagging on its second appearance, and the old "more than one
        // violation" filter made that invisible. Ordered by most recently seen so the cap drops
        // the coldest history first.
        var knownVehicleRows = await _db.Vehicles
            .Where(v => v.PropertyId == propertyId)
            .OrderByDescending(v => v.LastSeen)
            .Select(v => new
            {
                v.PlateNumber, v.PlateState, v.Make, v.Model, v.Color,
                v.ViolationCount, v.FirstSeen, v.LastSeen
            })
            .ToListAsync(ct);

        var knownVehicles = knownVehicleRows
            .Where(v => PlateNumber.IsTrackable(v.PlateNumber))
            .Take(250)
            .Select(v => new KnownVehicle(
                v.PlateNumber, v.PlateState, v.Make, v.Model, v.Color,
                v.ViolationCount, Local(v.FirstSeen), Local(v.LastSeen)))
            .ToList();

        var recent = incidents90
            .OrderByDescending(i => Effective(i.IncidentTime, i.CreatedAt))
            .Take(100)
            .Select(i => new IncidentSnapshot(
                Local(Effective(i.IncidentTime, i.CreatedAt)),
                i.IncidentType, i.Severity, i.Location, i.Description))
            .ToList();

        return new PropertyHistoryContext(
            propertyName,
            timeZoneId,
            Local(now)!,
            reportStats?.Count ?? 0,
            reportStats?.Earliest,
            reportStats?.Latest,
            incidents90.Count,
            averagePerReport,
            daysSinceSerious,
            lastSerious?.Description,
            countsByType,
            repeatAddressList,
            knownVehicles,
            recent);
    }
}
