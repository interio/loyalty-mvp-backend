using System.Text.Json;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Products.Domain;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Loyalty.Api.Modules.Products.Infrastructure.Persistence;

/// <summary>DbContext for products catalog.</summary>
public class ProductsDbContext : DbContext
{
    public ProductsDbContext(DbContextOptions<ProductsDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Distributor> Distributors => Set<Distributor>();
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var comparer = new ValueComparer<JsonObject>(
            (l, r) => JsonNode.DeepEquals(l, r),
            v => JsonSerializer.Serialize(v, new JsonSerializerOptions()).GetHashCode(),
            v => JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(v, new JsonSerializerOptions()), new JsonSerializerOptions())!);

        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.ToTable("Tenants", b => b.ExcludeFromMigrations());
        });

        // Ignore unrelated entities reachable via Tenant navigations.
        modelBuilder.Ignore<Customer>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<PointsAccount>();
        modelBuilder.Ignore<PointsTransaction>();

        modelBuilder.Entity<Distributor>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.ToTable("Distributors");
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired();
            e.Property(x => x.DistributorId).IsRequired();
            e.Property(x => x.Sku).IsRequired().HasMaxLength(200);
            e.Property(x => x.Gtin).HasMaxLength(50);
            e.Property(x => x.Name).IsRequired().HasMaxLength(400);
            e.Property(x => x.Cost).HasPrecision(18, 2);

            e.Property(x => x.Attributes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<JsonObject>(v, new JsonSerializerOptions())!)
                .Metadata.SetValueComparer(comparer);

            // Enforce uniqueness per tenant:
            // - Tenant + Distributor + SKU where GTIN is null.
            // - Tenant + Distributor + SKU + GTIN where GTIN is provided.
            e.HasIndex(x => new { x.TenantId, x.DistributorId, x.Sku, x.Gtin })
                .IsUnique()
                .HasFilter("\"Gtin\" IS NOT NULL");
            e.HasIndex(x => new { x.TenantId, x.DistributorId, x.Sku })
                .IsUnique()
                .HasFilter("\"Gtin\" IS NULL");

            e.HasOne(x => x.Distributor)
                .WithMany(x => x.Products)
                .HasForeignKey(x => new { x.TenantId, x.DistributorId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            e.ToTable("Products");
        });
    }
}
