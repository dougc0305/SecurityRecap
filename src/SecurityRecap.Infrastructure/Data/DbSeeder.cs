using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

namespace SecurityRecap.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.MigrateAsync();

        if (await db.Tenants.AnyAsync())
            return;

        // Create default tenant
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Demo HOA",
            Slug = "demo-hoa",
            Plan = PlanType.Hoa,
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Create admin user
        var admin = new ApplicationUser
        {
            UserName = "admin@securityrecap.com",
            Email = "admin@securityrecap.com",
            FullName = "System Admin",
            TenantId = tenant.Id,
            Role = UserRole.Admin,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, "Admin123!");
        if (!result.Succeeded)
            throw new Exception($"Failed to create seed admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
}
