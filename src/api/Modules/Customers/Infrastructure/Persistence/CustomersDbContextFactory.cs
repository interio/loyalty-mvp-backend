using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Modules.Customers.Infrastructure.Persistence;

/// <summary>Design-time factory for customers context.</summary>
public sealed class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CustomersDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("Missing ConnectionStrings__Default environment variable.");
        }

        optionsBuilder.UseNpgsql(cs);
        return new CustomersDbContext(optionsBuilder.Options);
    }
}
