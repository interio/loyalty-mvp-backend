using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Tenants.Domain;

namespace Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;

/// <summary>DbContext for ledger (accounts and transactions).</summary>
public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

    public DbSet<PointsAccount> PointsAccounts => Set<PointsAccount>();
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Ignore tenant entity to avoid accidental table creation in this context.
        modelBuilder.Ignore<Tenant>();

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.ToTable("Customers", b => b.ExcludeFromMigrations());
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired().HasMaxLength(320);
            e.ToTable("Users", b => b.ExcludeFromMigrations());
        });

        modelBuilder.Entity<PointsAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CustomerId).IsUnique();
            e.ToTable("PointsAccounts");
        });

        modelBuilder.Entity<PointsTransaction>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Reason).IsRequired().HasMaxLength(200);
            e.Property(x => x.CorrelationId).HasMaxLength(200);

            e.HasOne(x => x.Customer)
                .WithMany(c => c.Transactions)
                .HasForeignKey(x => x.CustomerId);

            e.HasOne(x => x.ActorUser)
                .WithMany(u => u.InitiatedTransactions)
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.CorrelationId);
            e.HasIndex(x => new { x.CustomerId, x.CorrelationId }).IsUnique();
            e.ToTable("PointsTransactions");
        });
    }
}
