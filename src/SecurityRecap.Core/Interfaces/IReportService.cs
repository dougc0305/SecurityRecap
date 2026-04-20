namespace SecurityRecap.Core.Interfaces;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

public interface IReportService
{
    Task<(IEnumerable<Report> Items, int TotalCount)> GetAllAsync(Guid tenantId, Guid userId, UserRole userRole, Guid? propertyId, int page, int pageSize);
    Task<Report?> GetByIdAsync(Guid tenantId, Guid userId, UserRole userRole, Guid id);
}
