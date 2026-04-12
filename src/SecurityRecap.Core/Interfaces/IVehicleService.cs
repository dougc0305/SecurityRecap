namespace SecurityRecap.Core.Interfaces;
using SecurityRecap.Core.Entities;

public interface IVehicleService
{
    Task<(IEnumerable<Vehicle> Items, int TotalCount)> GetAllAsync(Guid tenantId, Guid? propertyId, string? plate, int page, int pageSize);
    Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id);
}
