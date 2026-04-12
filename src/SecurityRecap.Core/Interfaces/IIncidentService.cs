namespace SecurityRecap.Core.Interfaces;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

public interface IIncidentService
{
    Task<(IEnumerable<Incident> Items, int TotalCount)> GetAllAsync(
        Guid tenantId, Guid? propertyId, IncidentType? type, Severity? severity,
        DateTime? from, DateTime? to, int page, int pageSize);
}
