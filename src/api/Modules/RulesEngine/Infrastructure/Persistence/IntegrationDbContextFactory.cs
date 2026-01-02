using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;

/// <summary>Design-time factory for IntegrationDbContext.</summary>
public sealed class IntegrationDbContextFactory : IDesignTimeDbContextFactory<IntegrationDbContext>
{
    public IntegrationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Host=postgres;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty";

        optionsBuilder.UseNpgsql(cs);
        return new IntegrationDbContext(optionsBuilder.Options);
    }
}
