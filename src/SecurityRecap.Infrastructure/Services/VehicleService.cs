using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
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
        Guid tenantId, Guid? propertyId, string? plate, int page, int pageSize)
    {
        var query = _db.Vehicles
            .Include(v => v.Property)
            .Where(v => v.Property.TenantId == tenantId);

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

    public async Task<Vehicle?> GetByIdAsync(Guid tenantId, Guid id)
    {
        return await _db.Vehicles
            .Include(v => v.Violations)
            .Where(v => v.Id == id && v.Property.TenantId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }
}
