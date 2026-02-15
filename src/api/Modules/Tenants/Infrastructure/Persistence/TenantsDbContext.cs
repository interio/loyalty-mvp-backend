using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;

/// <summary>DbContext for tenants.</summary>
public class TenantsDbContext : DbContext
{
    public TenantsDbContext(DbContextOptions<TenantsDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Ignore(x => x.Customers);
            e.Ignore(x => x.Users);
        });
    }
}
