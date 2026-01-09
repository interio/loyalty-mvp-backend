using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Modules.RewardOrders.Infrastructure.Persistence;

/// <summary>Design-time factory for RewardOrdersDbContext.</summary>
public sealed class RewardOrdersDbContextFactory : IDesignTimeDbContextFactory<RewardOrdersDbContext>
{
    public RewardOrdersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RewardOrdersDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("Missing ConnectionStrings__Default environment variable.");
        }

        optionsBuilder.UseNpgsql(cs);
        return new RewardOrdersDbContext(optionsBuilder.Options);
    }
}
