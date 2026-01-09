using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;

/// <summary>Design-time factory for tenants context.</summary>
public sealed class TenantsDbContextFactory : IDesignTimeDbContextFactory<TenantsDbContext>
{
    public TenantsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantsDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("Missing ConnectionStrings__Default environment variable.");
        }

        optionsBuilder.UseNpgsql(cs);
        return new TenantsDbContext(optionsBuilder.Options);
    }
}
