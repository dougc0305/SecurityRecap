namespace SecurityRecap.Core.Interfaces;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

public interface IPropertyService
{
    Task<IEnumerable<Property>> GetAllAsync(Guid tenantId, Guid userId, UserRole userRole);
    Task<Property?> GetByIdAsync(Guid tenantId, Guid userId, UserRole userRole, Guid id);
    Task<Property> CreateAsync(Guid tenantId, Property property);
    Task<Property> UpdateAsync(Guid tenantId, Guid id, Property property);
}
