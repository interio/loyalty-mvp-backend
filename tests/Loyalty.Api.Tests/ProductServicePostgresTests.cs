using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Domain;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Domain;
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
        await TestDbContextFactory.EnsureProductsSchemaAsync(productsDb);

        var service = new ProductService(productsDb);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var distributor = Guid.NewGuid();
        var otherDistributor = Guid.NewGuid();

        productsDb.Tenants.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant A" },
            new Tenant { Id = otherTenantId, Name = "Tenant B" });
        productsDb.Distributors.AddRange(
            new Distributor
            {
                Id = distributor,
                TenantId = tenantId,
                Name = "dist-a",
                DisplayName = "Distributor A"
            },
            new Distributor
            {
                Id = otherDistributor,
                TenantId = otherTenantId,
                Name = "dist-b",
                DisplayName = "Distributor B"
            });
        await productsDb.SaveChangesAsync();

        await service.UpsertAsync(new[]
        {
            new ProductUpsertRequest
            {
                TenantId = tenantId,
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
                TenantId = tenantId,
                DistributorId = distributor,
                Sku = "SKU-200",
                Name = "Cider Pack",
                Gtin = "5678",
                Cost = 5m
            },
            new ProductUpsertRequest
            {
                TenantId = otherTenantId,
                DistributorId = otherDistributor,
                Sku = "SKU-100",
                Name = "Other Tenant Product",
                Gtin = "9999",
                Cost = 7m
            }
        });

        var byName = await service.SearchAsync(tenantId, "Heineken");
        Assert.Single(byName);

        var bySku = await service.SearchAsync(tenantId, "SKU-200");
        Assert.Single(bySku);

        var byGtin = await service.SearchAsync(tenantId, "1234");
        Assert.Single(byGtin);

        var otherTenantByName = await service.SearchAsync(otherTenantId, "Heineken");
        Assert.Empty(otherTenantByName);

        var empty = await service.SearchAsync(tenantId, " ");
        Assert.Empty(empty);

        var list = await service.ListAsync(tenantId);
        Assert.Equal(2, list.Count);

        var otherTenantList = await service.ListAsync(otherTenantId);
        Assert.Single(otherTenantList);
    }

    [Fact]
    public async Task UpsertAsync_AllowsDistributorName()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var productsDb = TestDbContextFactory.CreateProducts(db.ConnectionString);
        await TestDbContextFactory.EnsureProductsSchemaAsync(productsDb);

        var service = new ProductService(productsDb);
        var tenantId = Guid.NewGuid();
        var distributorId = Guid.NewGuid();

        productsDb.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant A" });
        productsDb.Distributors.Add(new Distributor
        {
            Id = distributorId,
            TenantId = tenantId,
            Name = "dist-a",
            DisplayName = "Distributor A"
        });
        await productsDb.SaveChangesAsync();

        await service.UpsertAsync(new[]
        {
            new ProductUpsertRequest
            {
                TenantId = tenantId,
                DistributorName = "DIST-A",
                Sku = "SKU-NAME-1",
                Name = "Name Based Product",
                Cost = 8m
            }
        });

        var saved = await productsDb.Products.SingleAsync();
        Assert.Equal(distributorId, saved.DistributorId);
        Assert.Equal("Name Based Product", saved.Name);
    }

    [Fact]
    public async Task UpsertAsync_ValidatesRequest()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ProductsDbContext(options);
        var service = new ProductService(db);

        var exTenant = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new ProductUpsertRequest
                {
                    TenantId = Guid.Empty,
                    DistributorId = Guid.NewGuid(),
                    Sku = "SKU",
                    Name = "Name",
                    Cost = 1m
                }
            }));
        Assert.Contains("TenantId is required", exTenant.Message);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new ProductUpsertRequest
                {
                    TenantId = Guid.NewGuid(),
                    DistributorId = Guid.Empty,
                    DistributorName = " ",
                    Sku = " ",
                    Name = " ",
                    Cost = -1m
                }
            }));

        Assert.Contains("DistributorName or DistributorId is required", ex.Message);

        var exSku = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new ProductUpsertRequest
                {
                    TenantId = Guid.NewGuid(),
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
                    TenantId = Guid.NewGuid(),
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
                    TenantId = Guid.NewGuid(),
                    DistributorId = Guid.NewGuid(),
                    Sku = "SKU",
                    Name = "Name",
                    Cost = -5m
                }
            }));
        Assert.Contains("Cost cannot be negative", exCost.Message);
    }

    [Fact]
    public async Task UpsertAsync_ThrowsWhenDistributorIdAndNameMismatch()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var productsDb = TestDbContextFactory.CreateProducts(db.ConnectionString);
        await TestDbContextFactory.EnsureProductsSchemaAsync(productsDb);

        var service = new ProductService(productsDb);
        var tenantId = Guid.NewGuid();
        var distributorId = Guid.NewGuid();

        productsDb.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant A" });
        productsDb.Distributors.Add(new Distributor
        {
            Id = distributorId,
            TenantId = tenantId,
            Name = "dist-a",
            DisplayName = "Distributor A"
        });
        await productsDb.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new ProductUpsertRequest
                {
                    TenantId = tenantId,
                    DistributorId = Guid.NewGuid(),
                    DistributorName = "dist-a",
                    Sku = "SKU-MISMATCH",
                    Name = "Mismatch Product",
                    Cost = 1m
                }
            }));

        Assert.Contains("DistributorId does not match the provided distributorName for this tenant", ex.Message);
    }
}
