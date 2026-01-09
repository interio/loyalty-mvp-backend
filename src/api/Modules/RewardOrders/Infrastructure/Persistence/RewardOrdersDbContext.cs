using Loyalty.Api.Modules.RewardOrders.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RewardOrders.Infrastructure.Persistence;

/// <summary>DbContext for reward orders.</summary>
public class RewardOrdersDbContext : DbContext
{
    public RewardOrdersDbContext(DbContextOptions<RewardOrdersDbContext> options) : base(options) { }

    public DbSet<RewardOrder> RewardOrders => Set<RewardOrder>();
    public DbSet<RewardOrderItem> RewardOrderItems => Set<RewardOrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RewardOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderReference).HasMaxLength(200);
            e.Property(x => x.TotalPoints).IsRequired();
            e.Property(x => x.Status).IsRequired();
            e.Property(x => x.PlacedOnBehalf).HasDefaultValue(false);
            e.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.RewardOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.ToTable("RewardOrders");
        });

        modelBuilder.Entity<RewardOrderItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RewardVendor).IsRequired().HasMaxLength(200);
            e.Property(x => x.Sku).IsRequired().HasMaxLength(200);
            e.Property(x => x.Name).IsRequired().HasMaxLength(400);
            e.Property(x => x.PointsCost).IsRequired();
            e.Property(x => x.TotalPoints).IsRequired();
            e.ToTable("RewardOrderItems");
        });
    }
}
