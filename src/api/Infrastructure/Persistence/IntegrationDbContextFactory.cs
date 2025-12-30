using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for IntegrationDbContext (migrations).
/// </summary>
public sealed class IntegrationDbContextFactory : IDesignTimeDbContextFactory<IntegrationDbContext>
{
    /// <summary>Create a design-time IntegrationDbContext for migrations.</summary>
    public IntegrationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntegrationDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Host=postgres;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty";

        optionsBuilder.UseNpgsql(cs);
        return new IntegrationDbContext(optionsBuilder.Options);
    }
}
