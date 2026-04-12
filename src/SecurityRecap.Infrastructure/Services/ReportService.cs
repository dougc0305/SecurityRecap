using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Interfaces;
using SecurityRecap.Infrastructure.Data;

namespace SecurityRecap.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(IEnumerable<Report> Items, int TotalCount)> GetAllAsync(
        Guid tenantId, Guid? propertyId, int page, int pageSize)
    {
        var query = _db.Reports
            .Include(r => r.Property)
            .Where(r => r.Property.TenantId == tenantId);

        if (propertyId.HasValue)
            query = query.Where(r => r.PropertyId == propertyId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.ReportDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Report?> GetByIdAsync(Guid tenantId, Guid id)
    {
        return await _db.Reports
            .Include(r => r.Incidents)
            .Where(r => r.Id == id && r.Property.TenantId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }
}
