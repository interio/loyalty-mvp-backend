using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Domain;
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

        var tenantId = Guid.NewGuid();
        var distributor = Guid.NewGuid();
        db.Distributors.Add(new Distributor
        {
            Id = distributor,
            TenantId = tenantId,
            Name = "dist-1",
            DisplayName = "Distributor 1"
        });
        await db.SaveChangesAsync();

        await service.UpsertAsync(new[]
        {
            new ProductUpsertRequest
            {
                TenantId = tenantId,
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
                TenantId = tenantId,
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

    [Fact]
    public async Task Upsert_AllowsNullCost()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ProductsDbContext(options);
        var service = new ProductService(db);

        var tenantId = Guid.NewGuid();
        var distributor = Guid.NewGuid();
        db.Distributors.Add(new Distributor
        {
            Id = distributor,
            TenantId = tenantId,
            Name = "dist-1",
            DisplayName = "Distributor 1"
        });
        await db.SaveChangesAsync();

        await service.UpsertAsync(new[]
        {
            new ProductUpsertRequest
            {
                TenantId = tenantId,
                DistributorId = distributor,
                Sku = "SKU-NULL-COST",
                Name = "No Cost Product",
                Gtin = null,
                Cost = null
            }
        });

        var product = await db.Products.SingleAsync();
        Assert.Null(product.Cost);
    }

    [Fact]
    public async Task Upsert_ThrowsWhenDistributorMissingForTenant()
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
                    TenantId = Guid.NewGuid(),
                    DistributorId = Guid.NewGuid(),
                    Sku = "SKU-1",
                    Name = "Product",
                    Cost = 1m
                }
            }));

        Assert.Contains("Distributor not found for this tenant", ex.Message);
    }
}
