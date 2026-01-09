using System.Text.Json;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;

/// <summary>DbContext for reward catalog and inventory.</summary>
public class RewardCatalogDbContext : DbContext
{
    public RewardCatalogDbContext(DbContextOptions<RewardCatalogDbContext> options) : base(options) { }

    public DbSet<RewardProduct> RewardProducts => Set<RewardProduct>();
    public DbSet<RewardInventory> RewardInventories => Set<RewardInventory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var comparer = new ValueComparer<JsonObject>(
            (l, r) => JsonNode.DeepEquals(l, r),
            v => JsonSerializer.Serialize(v, new JsonSerializerOptions()).GetHashCode(),
            v => JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(v, new JsonSerializerOptions()), new JsonSerializerOptions())!);

        modelBuilder.Entity<RewardProduct>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RewardVendor).IsRequired().HasMaxLength(200);
            e.Property(x => x.Sku).IsRequired().HasMaxLength(200);
            e.Property(x => x.Gtin).HasMaxLength(50);
            e.Property(x => x.Name).IsRequired().HasMaxLength(400);
            e.Property(x => x.PointsCost).IsRequired();

            e.Property(x => x.Attributes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<JsonObject>(v, new JsonSerializerOptions())!)
                .Metadata.SetValueComparer(comparer);

            e.HasIndex(x => new { x.RewardVendor, x.Sku, x.Gtin })
                .IsUnique()
                .HasFilter("\"Gtin\" IS NOT NULL");
            e.HasIndex(x => new { x.RewardVendor, x.Sku })
                .IsUnique()
                .HasFilter("\"Gtin\" IS NULL");
            e.ToTable("RewardProducts");
        });

        modelBuilder.Entity<RewardInventory>(e =>
        {
            e.HasKey(x => x.RewardProductId);
            e.Property(x => x.AvailableQuantity).IsRequired();
            e.ToTable("RewardInventories");
        });
    }
}
