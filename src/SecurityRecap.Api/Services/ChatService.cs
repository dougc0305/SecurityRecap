using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Api.Prompts;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Api.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IClaudeApiService _claudeApi;
    private readonly ILogger<ChatService> _logger;

    public ChatService(AppDbContext db, IClaudeApiService claudeApi, ILogger<ChatService> logger)
    {
        _db = db;
        _claudeApi = claudeApi;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(
        Guid tenantId, Guid userId, UserRole userRole, Guid propertyId, string message,
        IEnumerable<ChatMessage>? conversationHistory)
    {
        // Verify property belongs to tenant
        var propertyExists = await _db.Properties
            .ApplyPropertyAccess(tenantId, userId, userRole)
            .AnyAsync(p => p.Id == propertyId);
        if (!propertyExists)
            throw new KeyNotFoundException($"Property {propertyId} not found for tenant");

        // Gather last 90 days of incidents
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
        var incidents = await _db.Incidents
            .Where(i => i.PropertyId == propertyId && i.CreatedAt >= ninetyDaysAgo)
            .OrderByDescending(i => i.IncidentTime)
            .Select(i => new
            {
                i.IncidentTime,
                IncidentType = i.IncidentType.ToString(),
                Severity = i.Severity.ToString(),
                i.Location,
                i.Description,
                i.OfficerName,
                i.LawEnforcement,
                i.CaseNumber
            })
            .Take(500)
            .AsNoTracking()
            .ToListAsync();

        // Gather aggregated statistics
        var stats = new
        {
            TotalIncidents = incidents.Count,
            ByType = incidents.GroupBy(i => i.IncidentType)
                .ToDictionary(g => g.Key, g => g.Count()),
            BySeverity = incidents.GroupBy(i => i.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopLocations = incidents
                .Where(i => !string.IsNullOrWhiteSpace(i.Location))
                .GroupBy(i => i.Location)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key!, g => g.Count()),
            LawEnforcementCalls = incidents.Count(i => i.LawEnforcement),
            VehicleCount = await _db.Vehicles
                .CountAsync(v => v.PropertyId == propertyId),
            RecentReportCount = await _db.Reports
                .CountAsync(r => r.PropertyId == propertyId && r.CreatedAt >= ninetyDaysAgo)
        };

        var incidentHistoryJson = JsonSerializer.Serialize(incidents);
        var propertyStatsJson = JsonSerializer.Serialize(stats);
        var systemPrompt = ChatPrompt.BuildSystemPrompt(incidentHistoryJson, propertyStatsJson);

        // Build message list: conversation history + current message
        var messages = new List<ChatMessage>();
        if (conversationHistory != null)
            messages.AddRange(conversationHistory);
        messages.Add(new ChatMessage("user", message));

        _logger.LogInformation(
            "Chat request for property {PropertyId}: {MessageCount} messages, {IncidentCount} incidents in context",
            propertyId, messages.Count, incidents.Count);

        return await _claudeApi.ChatAsync(systemPrompt, messages);
    }
}
