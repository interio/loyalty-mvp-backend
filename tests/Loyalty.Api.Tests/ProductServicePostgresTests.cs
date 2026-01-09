using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Domain;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class ProductServicePostgresTests
{
    [Fact]
    public async Task SearchAsync_FindsByNameSkuOrGtin()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var productsDb = TestDbContextFactory.CreateProducts(db.ConnectionString);
        await productsDb.Database.EnsureCreatedAsync();

        var service = new ProductService(productsDb);
        var distributor = Guid.NewGuid();

        await service.UpsertAsync(new[]
        {
            new ProductUpsertRequest
            {
                DistributorId = distributor,
                Sku = "SKU-100",
                Name = "Heineken Keg",
                Gtin = "1234",
                Cost = 10m,
                Attributes = new Dictionary<string, object?>
                {
                    ["text"] = "value",
                    ["num"] = 5,
                    ["flag"] = true,
                    ["other"] = new object()
                }
            },
            new ProductUpsertRequest
            {
                DistributorId = distributor,
                Sku = "SKU-200",
                Name = "Cider Pack",
                Gtin = "5678",
                Cost = 5m
            }
        });

        var byName = await service.SearchAsync("Heineken");
        Assert.Single(byName);

        var bySku = await service.SearchAsync("SKU-200");
        Assert.Single(bySku);

        var byGtin = await service.SearchAsync("1234");
        Assert.Single(byGtin);

        var empty = await service.SearchAsync(" ");
        Assert.Empty(empty);

        var list = await service.ListAsync();
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task UpsertAsync_ValidatesRequest()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ProductsDbContext(options);
        var service = new ProductService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new ProductUpsertRequest
                {
                    DistributorId = Guid.Empty,
                    Sku = " ",
                    Name = " ",
                    Cost = -1m
                }
            }));

        Assert.Contains("DistributorId is required", ex.Message);

        var exSku = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new ProductUpsertRequest
                {
                    DistributorId = Guid.NewGuid(),
                    Sku = " ",
                    Name = "Name",
                    Cost = 1m
                }
            }));
        Assert.Contains("Sku is required", exSku.Message);

        var exName = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new ProductUpsertRequest
                {
                    DistributorId = Guid.NewGuid(),
                    Sku = "SKU",
                    Name = " ",
                    Cost = 1m
                }
            }));
        Assert.Contains("Name is required", exName.Message);

        var exCost = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new ProductUpsertRequest
                {
                    DistributorId = Guid.NewGuid(),
                    Sku = "SKU",
                    Name = "Name",
                    Cost = -5m
                }
            }));
        Assert.Contains("Cost cannot be negative", exCost.Message);
    }
}
