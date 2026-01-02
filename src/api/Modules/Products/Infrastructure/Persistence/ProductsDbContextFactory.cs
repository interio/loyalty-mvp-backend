using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loyalty.Api.Modules.Products.Infrastructure.Persistence;

/// <summary>Design-time factory for ProductsDbContext.</summary>
public sealed class ProductsDbContextFactory : IDesignTimeDbContextFactory<ProductsDbContext>
{
    public ProductsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProductsDbContext>();

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Host=postgres;Port=5432;Database=loyalty;Username=loyalty;Password=loyalty";

        optionsBuilder.UseNpgsql(cs);
        return new ProductsDbContext(optionsBuilder.Options);
    }
}
