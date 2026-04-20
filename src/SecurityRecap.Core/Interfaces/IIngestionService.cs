namespace SecurityRecap.Core.Interfaces;

using SecurityRecap.Core.Enums;

public interface IIngestionService
{
    Task<Guid> IngestReportAsync(Guid tenantId, Guid userId, UserRole userRole, Guid propertyId, Stream pdfStream, string fileName);
}
