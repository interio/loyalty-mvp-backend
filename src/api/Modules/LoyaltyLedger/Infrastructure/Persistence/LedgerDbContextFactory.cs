using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;

/// <summary>Design-time factory for ledger context.</summary>
public sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LedgerDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("Missing ConnectionStrings__Default environment variable.");
        }

        optionsBuilder.UseNpgsql(cs);
        return new LedgerDbContext(optionsBuilder.Options);
    }
}
