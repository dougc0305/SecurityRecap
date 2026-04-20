namespace SecurityRecap.Core.Interfaces;

using SecurityRecap.Core.Enums;

public record IngestionOutcome(Guid ReportId, bool AlreadyIngested);

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
