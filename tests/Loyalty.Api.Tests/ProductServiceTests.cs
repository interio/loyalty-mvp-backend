using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task Upsert_WithNullGtinUsesDistributorAndSku()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ProductsDbContext(options);
        var service = new ProductService(db);

        var distributor = Guid.NewGuid();

        await service.UpsertAsync(new[]
        {
            new ProductUpsertRequest
            {
                DistributorId = distributor,
                Sku = "SKU-1",
                Name = "First",
                Gtin = null,
                Cost = 1m
            }
        });

        await service.UpsertAsync(new[]
        {
            new ProductUpsertRequest
            {
                DistributorId = distributor,
                Sku = "SKU-1",
                Name = "Updated",
                Gtin = null,
                Cost = 2m
            }
        });

        Assert.Equal(1, await db.Products.CountAsync());
        var product = await db.Products.FirstAsync();
        Assert.Equal("Updated", product.Name);
        Assert.Equal(2m, product.Cost);
    }
}
