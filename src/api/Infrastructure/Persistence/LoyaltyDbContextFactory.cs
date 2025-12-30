using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Infrastructure.Persistence;

/// <summary>
/// Provides EF Core with a reliable way to construct the DbContext at design-time
/// (migrations, database update) without depending on the web host startup.
/// </summary>
public sealed class LoyaltyDbContextFactory : IDesignTimeDbContextFactory<LoyaltyDbContext>
{
    /// <summary>
    /// Creates a design-time DbContext for migrations and tooling.
    /// </summary>
    public LoyaltyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoyaltyDbContext>();

        // Design-time: first try env var (works in containers/CI)
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        // Fallback for local dev container: compose service DNS name
        cs ??= "Host=postgres;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty";

        optionsBuilder.UseNpgsql(cs);
        return new LoyaltyDbContext(optionsBuilder.Options);
    }
}
