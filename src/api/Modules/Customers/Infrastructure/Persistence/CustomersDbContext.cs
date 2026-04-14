using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

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
        var addressComparer = new ValueComparer<CustomerAddress?>(
            (l, r) => AddressEquals(l, r),
            v => AddressHash(v),
            v => AddressSnapshot(v));

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
            e.Property(x => x.Tier).IsRequired().HasMaxLength(20).HasDefaultValue("bronze");
            e.Property(x => x.PhoneNumber).HasMaxLength(50);
            e.Property(x => x.Type).HasMaxLength(100);
            e.Property(x => x.BusinessSegment).HasMaxLength(120);
            e.Property(x => x.OnboardDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Status).HasDefaultValue(CustomerStatusCatalog.Active);
            e.Property(x => x.Address)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<CustomerAddress>(v, new JsonSerializerOptions()))
                .HasColumnType("jsonb")
                .Metadata.SetValueComparer(addressComparer);

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
            e.HasIndex(x => new { x.TenantId, x.CustomerId, x.ExternalId })
                .IsUnique()
                .HasFilter("\"ExternalId\" IS NOT NULL");
            e.ToTable("Users");
        });

        // Ignore ledger entities in this context.
        modelBuilder.Ignore<TenantConfigSetting>();
        modelBuilder.Ignore<PointsAccount>();
        modelBuilder.Ignore<PointsTransaction>();
    }

    private static bool AddressEquals(CustomerAddress? left, CustomerAddress? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return string.Equals(left.Address, right.Address, StringComparison.Ordinal) &&
               string.Equals(left.CountryCode, right.CountryCode, StringComparison.Ordinal) &&
               string.Equals(left.PostalCode, right.PostalCode, StringComparison.Ordinal) &&
               string.Equals(left.Region, right.Region, StringComparison.Ordinal);
    }

    private static int AddressHash(CustomerAddress? value) =>
        value is null
            ? 0
            : HashCode.Combine(value.Address, value.CountryCode, value.PostalCode, value.Region);

    private static CustomerAddress? AddressSnapshot(CustomerAddress? value) =>
        value is null
            ? null
            : new CustomerAddress
            {
                Address = value.Address,
                CountryCode = value.CountryCode,
                PostalCode = value.PostalCode,
                Region = value.Region
            };
}
