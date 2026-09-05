using Microsoft.EntityFrameworkCore;
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
        DateTime? IncidentTime,
        string IncidentType,
        string Severity,
        string? Location,
        string Description);

    public record TypeCount(string IncidentType, int Last7Days, int Last30Days, int Last90Days);

    public record RepeatAddress(string Address, int IncidentCount, DateTime? LastIncident, int Last30Days);

    public record RepeatVehicle(
        string PlateNumber,
        string? PlateState,
        string? Make,
        string? Model,
        string? Color,
        int ViolationCount,
        DateTime? FirstSeen,
        DateTime? LastSeen);

    public record PropertyHistoryContext(
        string PropertyName,
        DateTime GeneratedAtUtc,
        int ReportsOnFile,
        DateOnly? EarliestReportDate,
        DateOnly? LatestReportDate,
        int TotalIncidents90Days,
        double AverageIncidentsPerReport30Days,
        int? DaysSinceLastHighOrUrgent,
        string? LastHighOrUrgentDescription,
        IReadOnlyList<TypeCount> IncidentCountsByType,
        IReadOnlyList<RepeatAddress> RepeatAddresses,
        IReadOnlyList<RepeatVehicle> RepeatVehicles,
        IReadOnlyList<IncidentSnapshot> RecentIncidents);

    public async Task<PropertyHistoryContext> BuildAsync(Guid propertyId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since7 = now.AddDays(-7);
        var since30 = now.AddDays(-30);
        var since90 = now.AddDays(-90);

        var propertyName = await _db.Properties
            .Where(p => p.Id == propertyId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct) ?? "Unknown property";

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
                a.LastIncident,
                incidents90.Count(i =>
                    i.Location != null
                    && string.Equals(i.Location.Trim(), a.Address, StringComparison.OrdinalIgnoreCase)
                    && Effective(i.IncidentTime, i.CreatedAt) >= since30)))
            .ToList();

        var repeatVehicles = await _db.Vehicles
            .Where(v => v.PropertyId == propertyId && v.ViolationCount > 1)
            .OrderByDescending(v => v.ViolationCount)
            .Take(15)
            .Select(v => new RepeatVehicle(
                v.PlateNumber, v.PlateState, v.Make, v.Model, v.Color,
                v.ViolationCount, v.FirstSeen, v.LastSeen))
            .ToListAsync(ct);

        var recent = incidents90
            .OrderByDescending(i => Effective(i.IncidentTime, i.CreatedAt))
            .Take(100)
            .Select(i => new IncidentSnapshot(i.IncidentTime, i.IncidentType, i.Severity, i.Location, i.Description))
            .ToList();

        return new PropertyHistoryContext(
            propertyName,
            now,
            reportStats?.Count ?? 0,
            reportStats?.Earliest,
            reportStats?.Latest,
            incidents90.Count,
            averagePerReport,
            daysSinceSerious,
            lastSerious?.Description,
            countsByType,
            repeatAddressList,
            repeatVehicles,
            recent);
    }
}
