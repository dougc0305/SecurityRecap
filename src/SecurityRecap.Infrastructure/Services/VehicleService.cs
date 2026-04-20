using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Infrastructure.Services;

public class VehicleService : IVehicleService
{
    private readonly AppDbContext _db;

    public VehicleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(IEnumerable<Vehicle> Items, int TotalCount)> GetAllAsync(
        Guid tenantId, Guid userId, UserRole userRole, Guid? propertyId, string? plate, int page, int pageSize)
    {
        var accessiblePropertyIds = _db.AccessiblePropertyIds(tenantId, userId, userRole);

        var query = _db.Vehicles
            .Where(v => accessiblePropertyIds.Contains(v.PropertyId));

        if (propertyId.HasValue)
            query = query.Where(v => v.PropertyId == propertyId.Value);
        if (!string.IsNullOrWhiteSpace(plate))
            query = query.Where(v => v.PlateNumber.Contains(plate.ToUpperInvariant()));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(v => v.LastSeen)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid userId, UserRole userRole, Guid id)
    {
        var accessiblePropertyIds = _db.AccessiblePropertyIds(tenantId, userId, userRole);

        return await _db.Vehicles
            .Include(v => v.Violations)
            .Where(v => v.Id == id && accessiblePropertyIds.Contains(v.PropertyId))
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }
}
