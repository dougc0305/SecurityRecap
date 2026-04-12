using System.Text.Json.Serialization;

namespace SecurityRecap.Api.DTOs;

public class IngestionResult
{
    [JsonPropertyName("incidents")]
    public List<IngestionIncident> Incidents { get; set; } = new();

    [JsonPropertyName("vehicles")]
    public List<IngestionVehicle> Vehicles { get; set; } = new();

    [JsonPropertyName("maintenance_issues")]
    public List<IngestionMaintenanceIssue> MaintenanceIssues { get; set; } = new();

    [JsonPropertyName("pattern_matches")]
    public List<IngestionPatternMatch> PatternMatches { get; set; } = new();

    [JsonPropertyName("html_summary")]
    public string HtmlSummary { get; set; } = string.Empty;
}

public class IngestionIncident
{
    [JsonPropertyName("incident_time")]
    public string? IncidentTime { get; set; }

    [JsonPropertyName("incident_type")]
    public string IncidentType { get; set; } = "patrol";

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "low";

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("officer_name")]
    public string? OfficerName { get; set; }

    [JsonPropertyName("law_enforcement")]
    public bool LawEnforcement { get; set; }

    [JsonPropertyName("case_number")]
    public string? CaseNumber { get; set; }
}

public class IngestionVehicle
{
    [JsonPropertyName("plate_number")]
    public string PlateNumber { get; set; } = string.Empty;

    [JsonPropertyName("plate_state")]
    public string? PlateState { get; set; }

    [JsonPropertyName("make")]
    public string? Make { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("violation_type")]
    public string? ViolationType { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }
}

public class IngestionMaintenanceIssue
{
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "low";
}

public class IngestionPatternMatch
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonPropertyName("related_incidents")]
    public List<string> RelatedIncidents { get; set; } = new();

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = string.Empty;
}
