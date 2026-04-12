using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Infrastructure.Services;

public class PropertyService : IPropertyService
{
    private readonly AppDbContext _db;

    public PropertyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Property>> GetAllAsync(Guid tenantId)
    {
        return await _db.Properties
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Property?> GetByIdAsync(Guid tenantId, Guid id)
    {
        return await _db.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
    }

    public async Task<Property> CreateAsync(Guid tenantId, Property property)
    {
        property.Id = Guid.NewGuid();
        property.TenantId = tenantId;
        property.CreatedAt = DateTime.UtcNow;

        _db.Properties.Add(property);
        await _db.SaveChangesAsync();
        return property;
    }

    public async Task<Property> UpdateAsync(Guid tenantId, Guid id, Property updated)
    {
        var property = await _db.Properties
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId)
            ?? throw new KeyNotFoundException($"Property {id} not found");

        property.Name = updated.Name;
        property.Address = updated.Address;
        property.City = updated.City;
        property.State = updated.State;
        property.Zip = updated.Zip;
        property.SecurityCompany = updated.SecurityCompany;
        property.ReportEmail = updated.ReportEmail;
        property.Timezone = updated.Timezone;
        property.IsActive = updated.IsActive;

        await _db.SaveChangesAsync();
        return property;
    }
}
