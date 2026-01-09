using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;

/// <summary>Design-time factory for RewardCatalogDbContext.</summary>
public sealed class RewardCatalogDbContextFactory : IDesignTimeDbContextFactory<RewardCatalogDbContext>
{
    public RewardCatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RewardCatalogDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("Missing ConnectionStrings__Default environment variable.");
        }

        optionsBuilder.UseNpgsql(cs);
        return new RewardCatalogDbContext(optionsBuilder.Options);
    }
}
