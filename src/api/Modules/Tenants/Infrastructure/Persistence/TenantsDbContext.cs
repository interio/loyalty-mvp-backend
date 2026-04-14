using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;

/// <summary>DbContext for tenants.</summary>
public class TenantsDbContext : DbContext
{
    public TenantsDbContext(DbContextOptions<TenantsDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantConfigSetting> TenantConfigSettings => Set<TenantConfigSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Address).HasMaxLength(500);

            // Navigations from other modules are ignored in this context.
            e.Ignore(x => x.Customers);
            e.Ignore(x => x.Users);
        });

        modelBuilder.Entity<TenantConfigSetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ConfigName).IsRequired().HasMaxLength(120);
            e.Property(x => x.ConfigValue).IsRequired().HasMaxLength(2000);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.TenantId, x.ConfigName })
                .IsUnique()
                .HasFilter("\"TenantId\" IS NOT NULL");
            e.HasIndex(x => x.ConfigName)
                .IsUnique()
                .HasFilter("\"TenantId\" IS NULL");
            e.ToTable("TenantConfigSettings");
        });
    }
}
