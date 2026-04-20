namespace SecurityRecap.Core.Interfaces;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

public interface IAddressOfInterestService
{
    Task<(IEnumerable<AddressOfInterest> Items, int TotalCount)> GetAllAsync(
        Guid tenantId, Guid userId, UserRole userRole, Guid? propertyId, int page, int pageSize);

    Task<(AddressOfInterest? Address, IEnumerable<Incident> Incidents)> GetByIdAsync(
        Guid tenantId, Guid userId, UserRole userRole, Guid id);
}
