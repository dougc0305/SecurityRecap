namespace SecurityRecap.Core.Interfaces;
using SecurityRecap.Core.Entities;

public interface IPropertyService
{
    Task<IEnumerable<Property>> GetAllAsync(Guid tenantId);
    Task<Property?> GetByIdAsync(Guid tenantId, Guid id);
    Task<Property> CreateAsync(Guid tenantId, Property property);
    Task<Property> UpdateAsync(Guid tenantId, Guid id, Property property);
}
