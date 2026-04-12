namespace SecurityRecap.Core.Interfaces;

public interface IIngestionService
{
    Task<Guid> IngestReportAsync(Guid tenantId, Guid propertyId, Stream pdfStream, string fileName);
}
