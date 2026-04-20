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

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "palm-cove");
        if (tenant is null)
        {
            var demo = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "demo-hoa");
            if (demo is not null)
            {
                demo.Name = "Palm Cove HOA";
                demo.Slug = "palm-cove";
                tenant = demo;
            }
            else
            {
                tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = "Palm Cove HOA",
                    Slug = "palm-cove",
                    Plan = PlanType.Hoa,
                    IsActive = true
                };
                db.Tenants.Add(tenant);
            }
            await db.SaveChangesAsync();
        }

        var admin = await userManager.FindByEmailAsync("admin@securityrecap.com");
        if (admin is null)
        {
            admin = new ApplicationUser
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
        else if (admin.TenantId != tenant.Id)
        {
            admin.TenantId = tenant.Id;
            await userManager.UpdateAsync(admin);
        }

        var property = await db.Properties.FirstOrDefaultAsync(p => p.TenantId == tenant.Id && p.Name == "Palm Cove HOA");
        if (property is null)
        {
            property = new Property
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Palm Cove HOA",
                Address = "11501 Hutchison Blvd",
                City = "Panama City Beach",
                State = "FL",
                Zip = "32407",
                SecurityCompany = "Emerald Coast Security Agency",
                ReportEmail = "dougc@charb-tech.com",
                Timezone = "America/Chicago",
                IsActive = true
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();
        }

        var link = await db.UserProperties.FirstOrDefaultAsync(up => up.UserId == admin.Id && up.PropertyId == property.Id);
        if (link is null)
        {
            db.UserProperties.Add(new UserProperty { UserId = admin.Id, PropertyId = property.Id });
            await db.SaveChangesAsync();
        }

        var doug = await userManager.FindByEmailAsync("dougc@charb-tech.com");
        if (doug is null)
        {
            doug = new ApplicationUser
            {
                UserName = "dougc@charb-tech.com",
                Email = "dougc@charb-tech.com",
                FullName = "Doug Charbonneau",
                TenantId = tenant.Id,
                Role = UserRole.Admin,
                IsActive = true,
                EmailConfirmed = true
            };
            var dougResult = await userManager.CreateAsync(doug, "SecurityRecap2026!");
            if (!dougResult.Succeeded)
                throw new Exception($"Failed to create Doug: {string.Join(", ", dougResult.Errors.Select(e => e.Description))}");
        }
        else if (doug.TenantId != tenant.Id)
        {
            doug.TenantId = tenant.Id;
            await userManager.UpdateAsync(doug);
        }

        var tenantPropertyIds = await db.Properties
            .Where(p => p.TenantId == tenant.Id)
            .Select(p => p.Id)
            .ToListAsync();
        var dougLinks = await db.UserProperties
            .Where(up => up.UserId == doug.Id)
            .Select(up => up.PropertyId)
            .ToListAsync();
        foreach (var pid in tenantPropertyIds.Except(dougLinks))
        {
            db.UserProperties.Add(new UserProperty { UserId = doug.Id, PropertyId = pid });
        }
        await db.SaveChangesAsync();
    }
}
