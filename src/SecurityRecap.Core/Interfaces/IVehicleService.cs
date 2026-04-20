namespace SecurityRecap.Core.Interfaces;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

public interface IVehicleService
{
    Task<(IEnumerable<Vehicle> Items, int TotalCount)> GetAllAsync(Guid tenantId, Guid userId, UserRole userRole, Guid? propertyId, string? plate, int page, int pageSize);
    Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid userId, UserRole userRole, Guid id);
}
