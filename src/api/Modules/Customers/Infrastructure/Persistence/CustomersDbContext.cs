using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.Customers.Infrastructure.Persistence;

/// <summary>DbContext for customers and users.</summary>
public class CustomersDbContext : DbContext
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>(); // for FK mapping

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.ToTable("Tenants", b => b.ExcludeFromMigrations());
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.Property(x => x.ContactEmail).HasMaxLength(320);
            e.Property(x => x.ExternalId).HasMaxLength(200);

            e.HasOne(x => x.Tenant)
                .WithMany(t => t.Customers)
                .HasForeignKey(x => x.TenantId);

            e.HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique();
            e.ToTable("Customers");
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Email).IsRequired().HasMaxLength(320);
            e.Property(x => x.ExternalId).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(100);

            e.HasOne(x => x.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(x => x.TenantId);

            e.HasOne(x => x.Customer)
                .WithMany(c => c.Users)
                .HasForeignKey(x => x.CustomerId);

            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CustomerId, x.ExternalId });
            e.ToTable("Users");
        });

        // Ignore ledger entities in this context.
        modelBuilder.Ignore<PointsAccount>();
        modelBuilder.Ignore<PointsTransaction>();
    }
}
