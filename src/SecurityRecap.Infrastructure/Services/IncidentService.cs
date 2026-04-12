using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Infrastructure.Services;

public class IncidentService : IIncidentService
{
    private readonly AppDbContext _db;

    public IncidentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(IEnumerable<Incident> Items, int TotalCount)> GetAllAsync(
        Guid tenantId, Guid? propertyId, IncidentType? type, Severity? severity,
        DateTime? from, DateTime? to, int page, int pageSize)
    {
        var query = _db.Incidents
            .Include(i => i.Property)
            .Where(i => i.Property.TenantId == tenantId);

        if (propertyId.HasValue)
            query = query.Where(i => i.PropertyId == propertyId.Value);
        if (type.HasValue)
            query = query.Where(i => i.IncidentType == type.Value);
        if (severity.HasValue)
            query = query.Where(i => i.Severity == severity.Value);
        if (from.HasValue)
            query = query.Where(i => i.IncidentTime >= from.Value);
        if (to.HasValue)
            query = query.Where(i => i.IncidentTime <= to.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(i => i.IncidentTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }
}
