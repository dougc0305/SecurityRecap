using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Api.DTOs;
using SecurityRecap.Api.Prompts;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Api.Services;

public class IngestionService : IIngestionService
{
    private readonly AppDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly IClaudeApiService _claudeApi;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        AppDbContext db,
        IBlobStorageService blobStorage,
        IClaudeApiService claudeApi,
        ILogger<IngestionService> logger)
    {
        _db = db;
        _blobStorage = blobStorage;
        _claudeApi = claudeApi;
        _logger = logger;
    }

    public async Task<Guid> IngestReportAsync(Guid tenantId, Guid userId, UserRole userRole, Guid propertyId, Stream pdfStream, string fileName)
    {
        // Verify property belongs to tenant
        var property = await _db.Properties
            .ApplyPropertyAccess(tenantId, userId, userRole)
            .FirstOrDefaultAsync(p => p.Id == propertyId)
            ?? throw new KeyNotFoundException($"Property {propertyId} not found for tenant");

        // Read PDF into memory (needed for both blob upload and Claude API)
        using var memoryStream = new MemoryStream();
        await pdfStream.CopyToAsync(memoryStream);
        var pdfBytes = memoryStream.ToArray();

        // Upload PDF to blob storage
        memoryStream.Position = 0;
        var pdfUrl = await _blobStorage.UploadAsync(memoryStream, fileName, "application/pdf");
        _logger.LogInformation("Uploaded PDF to {Url}", pdfUrl);

        // Get recent incident history for context
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var recentIncidents = await _db.Incidents
            .Where(i => i.PropertyId == propertyId && i.CreatedAt >= thirtyDaysAgo)
            .OrderByDescending(i => i.IncidentTime)
            .Select(i => new
            {
                i.IncidentTime,
                IncidentType = i.IncidentType.ToString(),
                Severity = i.Severity.ToString(),
                i.Location,
                i.Description
            })
            .Take(100)
            .ToListAsync();

        var historyJson = JsonSerializer.Serialize(recentIncidents);
        var systemPrompt = IngestionPrompt.Build(historyJson);

        // Call Claude API
        _logger.LogInformation("Calling Claude API for property {PropertyId}", propertyId);
        var claudeResponse = await _claudeApi.AnalyzePdfAsync(pdfBytes, systemPrompt);

        // Strip markdown code fences if present
        claudeResponse = StripCodeFences(claudeResponse);

        // Parse the response
        var result = JsonSerializer.Deserialize<IngestionResult>(claudeResponse)
            ?? throw new InvalidOperationException("Failed to parse Claude API response");

        // Replace existing report for same property+date (cascades to incidents & violations)
        var reportDate = TryParseReportDate(result.ReportDate)
            ?? (TryParseDateTime(result.PeriodEnd) is { } pe ? DateOnly.FromDateTime(pe) : (DateOnly?)null)
            ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var existingReports = await _db.Reports
            .Where(r => r.PropertyId == propertyId && r.ReportDate == reportDate)
            .ToListAsync();
        if (existingReports.Count > 0)
        {
            _db.Reports.RemoveRange(existingReports);
            await _db.SaveChangesAsync();
            await RecomputeAggregatesAsync(propertyId);
            _logger.LogInformation("Replaced {Count} existing report(s) for property {PropertyId} on {Date}",
                existingReports.Count, propertyId, reportDate);
        }

        // Create report
        var report = new Report
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            ReportDate = reportDate,
            PeriodStart = TryParseDateTime(result.PeriodStart),
            PeriodEnd = TryParseDateTime(result.PeriodEnd),
            RawPdfUrl = pdfUrl,
            AiSummaryHtml = result.HtmlSummary,
            OfficerNames = result.Incidents
                .Where(i => !string.IsNullOrWhiteSpace(i.OfficerName))
                .Select(i => i.OfficerName!)
                .Distinct()
                .ToArray()
        };
        _db.Reports.Add(report);

        // Create incidents
        var createdIncidents = new List<Incident>();
        foreach (var inc in result.Incidents)
        {
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                PropertyId = propertyId,
                IncidentTime = TryParseDateTime(inc.IncidentTime),
                IncidentType = ParseIncidentType(inc.IncidentType),
                Severity = ParseSeverity(inc.Severity),
                Location = inc.Location,
                Description = inc.Description,
                OfficerName = inc.OfficerName,
                LawEnforcement = inc.LawEnforcement,
                CaseNumber = inc.CaseNumber
            };
            createdIncidents.Add(incident);
            _db.Incidents.Add(incident);
        }

        // Create or update vehicles and link violations
        foreach (var veh in result.Vehicles)
        {
            if (string.IsNullOrWhiteSpace(veh.PlateNumber))
                continue;

            var plateNormalized = veh.PlateNumber.Trim().ToUpperInvariant();

            // Find existing vehicle or create new
            var vehicle = await _db.Vehicles
                .FirstOrDefaultAsync(v => v.PropertyId == propertyId
                    && v.PlateNumber == plateNormalized);

            if (vehicle is null)
            {
                vehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    PropertyId = propertyId,
                    PlateNumber = plateNormalized,
                    PlateState = veh.PlateState,
                    Make = veh.Make,
                    Model = veh.Model,
                    Color = veh.Color,
                    FirstSeen = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    ViolationCount = 1
                };
                _db.Vehicles.Add(vehicle);
            }
            else
            {
                vehicle.LastSeen = DateTime.UtcNow;
                vehicle.ViolationCount++;
                // Update details if previously unknown
                vehicle.Make ??= veh.Make;
                vehicle.Model ??= veh.Model;
                vehicle.Color ??= veh.Color;
                vehicle.PlateState ??= veh.PlateState;
            }

            // Create violation linked to vehicle
            if (!string.IsNullOrWhiteSpace(veh.ViolationType))
            {
                var linkedIncident = ResolveViolationIncident(createdIncidents, veh.Location);
                if (linkedIncident is null)
                {
                    _logger.LogWarning(
                        "Skipping violation for vehicle {PlateNumber} in report {ReportId} because no matching incident was found",
                        plateNormalized,
                        report.Id);
                    continue;
                }

                _db.Violations.Add(new Violation
                {
                    Id = Guid.NewGuid(),
                    IncidentId = linkedIncident.Id,
                    VehicleId = vehicle.Id,
                    ViolationType = veh.ViolationType,
                    Location = veh.Location
                });
            }
        }

        // Upsert addresses of interest from incident locations
        var now = DateTime.UtcNow;
        var incidentsByAddress = createdIncidents
            .Where(i => !string.IsNullOrWhiteSpace(i.Location))
            .GroupBy(i => i.Location!.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var grp in incidentsByAddress)
        {
            var address = grp.Key;
            var latest = grp.Max(i => i.IncidentTime) ?? now;

            var existing = await _db.AddressesOfInterest
                .FirstOrDefaultAsync(a => a.PropertyId == propertyId
                    && a.Address.ToLower() == address.ToLower());

            if (existing is null)
            {
                _db.AddressesOfInterest.Add(new AddressOfInterest
                {
                    Id = Guid.NewGuid(),
                    PropertyId = propertyId,
                    Address = address,
                    IncidentCount = grp.Count(),
                    FirstFlagged = now,
                    LastIncident = latest
                });
            }
            else
            {
                existing.IncidentCount += grp.Count();
                existing.LastIncident = latest > (existing.LastIncident ?? DateTime.MinValue) ? latest : existing.LastIncident;
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Ingested report {ReportId}: {IncidentCount} incidents, {VehicleCount} vehicles, {AddressCount} addresses",
            report.Id, result.Incidents.Count, result.Vehicles.Count, incidentsByAddress.Count());

        return report.Id;
    }

    private static string StripCodeFences(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
                text = text[(firstNewline + 1)..];
            else
                return string.Empty;
        }
        if (text.EndsWith("```"))
            text = text[..^3];
        return text.Trim();
    }

    private static Incident? ResolveViolationIncident(IEnumerable<Incident> incidents, string? location)
    {
        var incidentList = incidents.ToList();
        if (incidentList.Count == 0)
            return null;

        if (incidentList.Count == 1)
            return incidentList[0];

        var normalizedLocation = NormalizeLocation(location);
        if (string.IsNullOrEmpty(normalizedLocation))
            return null;

        return incidentList.FirstOrDefault(i => NormalizeLocation(i.Location) == normalizedLocation);
    }

    private async Task RecomputeAggregatesAsync(Guid propertyId)
    {
        // Vehicles: recompute ViolationCount from remaining violations
        var vehicles = await _db.Vehicles.Where(v => v.PropertyId == propertyId).ToListAsync();
        foreach (var v in vehicles)
        {
            v.ViolationCount = await _db.Violations.CountAsync(x => x.VehicleId == v.Id);
        }

        // AddressesOfInterest: recompute from remaining incidents for this property
        var addresses = await _db.AddressesOfInterest.Where(a => a.PropertyId == propertyId).ToListAsync();
        _db.AddressesOfInterest.RemoveRange(addresses);

        var remainingByAddress = await _db.Incidents
            .Where(i => i.PropertyId == propertyId && i.Location != null && i.Location != "")
            .GroupBy(i => i.Location!)
            .Select(g => new { Address = g.Key, Count = g.Count(), Last = g.Max(i => i.IncidentTime) })
            .ToListAsync();

        foreach (var row in remainingByAddress)
        {
            _db.AddressesOfInterest.Add(new AddressOfInterest
            {
                Id = Guid.NewGuid(),
                PropertyId = propertyId,
                Address = row.Address,
                IncidentCount = row.Count,
                FirstFlagged = DateTime.UtcNow,
                LastIncident = row.Last
            });
        }

        await _db.SaveChangesAsync();
    }

    private static string NormalizeLocation(string? location)
    {
        return string.IsNullOrWhiteSpace(location)
            ? string.Empty
            : location.Trim().ToUpperInvariant();
    }

    private static DateOnly? TryParseReportDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParse(value, out var d) ? d : null;
    }

    private static DateTime? TryParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var dt) ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : null;
    }

    private static IncidentType ParseIncidentType(string value)
    {
        return value.ToLowerInvariant().Replace(" ", "_") switch
        {
            "noise" => IncidentType.Noise,
            "parking" => IncidentType.Parking,
            "maintenance" => IncidentType.Maintenance,
            "gate" => IncidentType.Gate,
            "law_enforcement" => IncidentType.LawEnforcement,
            "patrol" => IncidentType.Patrol,
            "phone_call" => IncidentType.PhoneCall,
            _ => IncidentType.Patrol
        };
    }

    private static Severity ParseSeverity(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "low" => Severity.Low,
            "medium" => Severity.Medium,
            "high" => Severity.High,
            "urgent" => Severity.Urgent,
            _ => Severity.Low
        };
    }
}
