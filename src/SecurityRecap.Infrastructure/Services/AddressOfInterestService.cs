using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Infrastructure.Services;

public class AddressOfInterestService : IAddressOfInterestService
{
    private readonly AppDbContext _db;

    public AddressOfInterestService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(IEnumerable<AddressOfInterest> Items, int TotalCount)> GetAllAsync(
        Guid tenantId, Guid userId, UserRole userRole, Guid? propertyId, int page, int pageSize)
    {
        var accessiblePropertyIds = _db.AccessiblePropertyIds(tenantId, userId, userRole);

        var query = _db.AddressesOfInterest
            .Where(a => accessiblePropertyIds.Contains(a.PropertyId));

        if (propertyId.HasValue)
            query = query.Where(a => a.PropertyId == propertyId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.IncidentCount)
            .ThenByDescending(a => a.LastIncident)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(AddressOfInterest? Address, IEnumerable<Incident> Incidents)> GetByIdAsync(
        Guid tenantId, Guid userId, UserRole userRole, Guid id)
    {
        var accessiblePropertyIds = _db.AccessiblePropertyIds(tenantId, userId, userRole);

        var address = await _db.AddressesOfInterest
            .Where(a => a.Id == id && accessiblePropertyIds.Contains(a.PropertyId))
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (address is null)
            return (null, Enumerable.Empty<Incident>());

        var incidents = await _db.Incidents
            .Where(i => i.PropertyId == address.PropertyId
                && i.Location != null
                && i.Location.ToLower() == address.Address.ToLower())
            .OrderByDescending(i => i.IncidentTime)
            .AsNoTracking()
            .ToListAsync();

        return (address, incidents);
    }
}
