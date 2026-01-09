using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;

/// <summary>Design-time factory for IntegrationDbContext.</summary>
public sealed class IntegrationDbContextFactory : IDesignTimeDbContextFactory<IntegrationDbContext>
{
    public IntegrationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("Missing ConnectionStrings__Default environment variable.");
        }

        optionsBuilder.UseNpgsql(cs);
        return new IntegrationDbContext(optionsBuilder.Options);
    }
}
