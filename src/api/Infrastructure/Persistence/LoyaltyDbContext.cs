using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Loyalty MVP.
/// </summary>
public class LoyaltyDbContext : DbContext
{
    /// <summary>Configured EF Core context for the modular monolith.</summary>
    public LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options) : base(options) { }

    /// <summary>Tenant module aggregate root set.</summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>Customer module aggregate root set.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Customer module users/employees set.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Ledger module cached balances.</summary>
    public DbSet<PointsAccount> PointsAccounts => Set<PointsAccount>();

    /// <summary>Ledger module immutable transactions.</summary>
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();

    /// <summary>Configure entity mappings and module boundaries.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
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

            // Strongly recommended for ERP matching:
            // ExternalId should be unique per tenant (if provided).
            e.HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique();

            // One-to-one cached points account.
            e.HasOne(x => x.PointsAccount)
                .WithOne(a => a.Customer)
                .HasForeignKey<PointsAccount>(a => a.CustomerId);
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

            // FK linking Users to Customers (yes, we want this).
            e.HasOne(x => x.Customer)
                .WithMany(c => c.Users)
                .HasForeignKey(x => x.CustomerId);

            // Login identity uniqueness per tenant.
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();

            // ERP links are at the customer level; allow reuse across users in a tenant.
            e.HasIndex(x => new { x.TenantId, x.CustomerId, x.ExternalId });
        });

        modelBuilder.Entity<PointsAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CustomerId).IsUnique();
        });

        modelBuilder.Entity<PointsTransaction>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Reason).IsRequired().HasMaxLength(200);
            e.Property(x => x.CorrelationId).HasMaxLength(200);

            e.HasOne(x => x.Customer)
                .WithMany(c => c.Transactions)
                .HasForeignKey(x => x.CustomerId);

            // Optional attribution to user. If user is deleted, we keep the ledger row.
            e.HasOne(x => x.ActorUser)
                .WithMany(u => u.InitiatedTransactions)
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.CorrelationId);

            // Recommended for idempotency checks (same correlation id should not be applied twice per customer).
            // Unique with nullable correlation id is OK in Postgres (multiple NULLs allowed).
            e.HasIndex(x => new { x.CustomerId, x.CorrelationId }).IsUnique();
        });
    }
}
