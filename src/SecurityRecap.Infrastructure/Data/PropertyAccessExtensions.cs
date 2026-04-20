using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

namespace SecurityRecap.Infrastructure.Data;

public static class PropertyAccessExtensions
{
    public static IQueryable<Property> ApplyPropertyAccess(
        this IQueryable<Property> query,
        Guid tenantId,
        Guid userId,
        UserRole userRole)
    {
        var tenantScoped = query.Where(p => p.TenantId == tenantId);

        return userRole == UserRole.Admin
            ? tenantScoped
            : tenantScoped.Where(p => p.UserProperties.Any(up => up.UserId == userId));
    }

    public static IQueryable<Guid> AccessiblePropertyIds(
        this AppDbContext db,
        Guid tenantId,
        Guid userId,
        UserRole userRole)
    {
        return db.Properties
            .ApplyPropertyAccess(tenantId, userId, userRole)
            .Select(p => p.Id);
    }
}