namespace SecurityRecap.Core.Entities;

public class Report
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public DateOnly ReportDate { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string? RawPdfUrl { get; set; }
    public string? MdSummaryUrl { get; set; }
    public string? AiSummaryHtml { get; set; }
    public string[] OfficerNames { get; set; } = Array.Empty<string>();
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
}
