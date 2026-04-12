namespace SecurityRecap.Core.Interfaces;
using SecurityRecap.Core.Entities;

public interface IReportService
{
    Task<(IEnumerable<Report> Items, int TotalCount)> GetAllAsync(Guid tenantId, Guid? propertyId, int page, int pageSize);
    Task<Report?> GetByIdAsync(Guid tenantId, Guid id);
}
