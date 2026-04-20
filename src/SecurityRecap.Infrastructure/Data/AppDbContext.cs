using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecurityRecap.Core.Entities;
using SecurityRecap.Core.Enums;

namespace SecurityRecap.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Violation> Violations => Set<Violation>();
    public DbSet<AddressOfInterest> AddressesOfInterest => Set<AddressOfInterest>();
    public DbSet<UserProperty> UserProperties => Set<UserProperty>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ServiceAssignment> ServiceAssignments => Set<ServiceAssignment>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Use snake_case for all tables and columns
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName()!));
            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
        }

        // Tenant
        builder.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.Plan).HasConversion<string>();
            e.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        });

        // Property
        builder.Entity<Property>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(p => p.Tenant).WithMany(t => t.Properties).HasForeignKey(p => p.TenantId);
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        });

        // Report
        builder.Entity<Report>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(r => r.Property).WithMany(p => p.Reports).HasForeignKey(r => r.PropertyId);
            e.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(r => new { r.PropertyId, r.ExternalId })
                .IsUnique()
                .HasFilter("external_id IS NOT NULL");
        });

        // Incident
        builder.Entity<Incident>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(i => i.Report).WithMany(r => r.Incidents).HasForeignKey(i => i.ReportId);
            e.HasOne(i => i.Property).WithMany(p => p.Incidents).HasForeignKey(i => i.PropertyId);
            e.Property(i => i.IncidentType).HasConversion<string>();
            e.Property(i => i.Severity).HasConversion<string>();
            e.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
        });

        // Vehicle
        builder.Entity<Vehicle>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(v => v.Property).WithMany(p => p.Vehicles).HasForeignKey(v => v.PropertyId);
            e.Property(v => v.CreatedAt).HasDefaultValueSql("now()");
        });

        // Violation
        builder.Entity<Violation>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(v => v.Incident).WithMany(i => i.Violations).HasForeignKey(v => v.IncidentId);
            e.HasOne(v => v.Vehicle).WithMany(v => v.Violations).HasForeignKey(v => v.VehicleId).IsRequired(false);
            e.Property(v => v.CreatedAt).HasDefaultValueSql("now()");
        });

        // AddressOfInterest
        builder.Entity<AddressOfInterest>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(a => a.Property).WithMany(p => p.AddressesOfInterest).HasForeignKey(a => a.PropertyId);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
        });

        // ApplicationUser
        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId);
            e.Property(u => u.Role).HasConversion<string>();
            e.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        });

        // UserProperty (join table)
        builder.Entity<UserProperty>(e =>
        {
            e.HasKey(up => new { up.UserId, up.PropertyId });
            e.HasOne(up => up.User).WithMany(u => u.UserProperties).HasForeignKey(up => up.UserId);
            e.HasOne(up => up.Property).WithMany(p => p.UserProperties).HasForeignKey(up => up.PropertyId);
        });

        // RefreshToken
        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);
            e.Property(rt => rt.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(rt => rt.User).WithMany().HasForeignKey(rt => rt.UserId);
            e.HasIndex(rt => rt.Token).IsUnique();
            e.Property(rt => rt.CreatedAt).HasDefaultValueSql("now()");
        });

        // ApiKey (tenant-scoped integration credential)
        builder.Entity<ApiKey>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId);
            e.HasIndex(a => a.KeyHash).IsUnique();
            e.HasIndex(a => a.TenantId);
            e.Property(a => a.CreatedAt).HasDefaultValueSql("now()");
        });

        // ServiceAssignment (cross-tenant property visibility for management/security companies)
        builder.Entity<ServiceAssignment>(e =>
        {
            e.HasKey(sa => sa.Id);
            e.Property(sa => sa.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(sa => sa.Property).WithMany().HasForeignKey(sa => sa.PropertyId);
            e.HasOne(sa => sa.Tenant).WithMany().HasForeignKey(sa => sa.TenantId);
            e.Property(sa => sa.Role).HasConversion<string>();
            e.Property(sa => sa.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(sa => new { sa.PropertyId, sa.TenantId, sa.Role }).IsUnique();
        });
    }

    private static string ToSnakeCase(string name)
    {
        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])
                ? "_" + c
                : c.ToString()
        )).ToLower();
    }
}
