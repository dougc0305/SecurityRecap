namespace SecurityRecap.Core.Interfaces;

using SecurityRecap.Core.Enums;

public record IngestionOutcome(
    Guid ReportId,
    bool AlreadyIngested,
    DateOnly? ReportDate = null,
    string? AiSummaryHtml = null,
    string? MarkdownSummary = null,
    int IncidentCount = 0,
    IReadOnlyList<string>? UrgentItems = null);

public interface IIngestionService
{
    Task<IngestionOutcome> IngestReportAsync(
        Guid tenantId,
        Guid userId,
        UserRole userRole,
        Guid propertyId,
        Stream pdfStream,
        string fileName,
        string? externalId = null);
}
