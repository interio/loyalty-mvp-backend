using System.Text.Json;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.Products.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Loyalty.Api.Modules.Products.Infrastructure.Persistence;

/// <summary>DbContext for products catalog.</summary>
public class ProductsDbContext : DbContext
{
    public ProductsDbContext(DbContextOptions<ProductsDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var comparer = new ValueComparer<JsonObject>(
            (l, r) => JsonNode.DeepEquals(l, r),
            v => v.GetHashCode(),
            v => JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(v, new JsonSerializerOptions()), new JsonSerializerOptions())!);

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).IsRequired().HasMaxLength(200);
            e.Property(x => x.Gtin).HasMaxLength(50);
            e.Property(x => x.Name).IsRequired().HasMaxLength(400);
            e.Property(x => x.Cost).HasPrecision(18, 2);

            e.Property(x => x.Attributes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<JsonObject>(v, new JsonSerializerOptions())!)
                .Metadata.SetValueComparer(comparer);

            // Uniqueness per distributor + SKU + GTIN.
            e.HasIndex(x => new { x.DistributorId, x.Sku, x.Gtin }).IsUnique();
            e.ToTable("Products");
        });
    }
}
