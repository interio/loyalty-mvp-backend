using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Loyalty.Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class RewardCatalogServiceTests
{
    [Fact]
    public async Task UpsertAsync_CreatesAndUpdatesProducts()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var catalogDb = TestDbContextFactory.CreateRewardCatalog(db.ConnectionString);
        await TestDbContextFactory.EnsureRewardCatalogSchemaAsync(catalogDb);

        var service = new RewardCatalogService(catalogDb);
        var tenantId = Guid.NewGuid();

        await service.UpsertAsync(new[]
        {
            new RewardProductUpsertRequest
            {
                TenantId = tenantId,
                RewardVendor = "VendorA",
                Sku = "SKU-1",
                Name = "Gift Card",
                PointsCost = 100,
                InventoryQuantity = 10,
                Attributes = new Dictionary<string, object?>
                {
                    ["text"] = "value",
                    ["num"] = 5,
                    ["flag"] = true,
                    ["other"] = new object()
                }
            }
        });

        await service.UpsertAsync(new[]
        {
            new RewardProductUpsertRequest
            {
                TenantId = tenantId,
                RewardVendor = "VendorA",
                Sku = "SKU-1",
                Name = "Gift Card Updated",
                PointsCost = 120,
                InventoryQuantity = 5
            }
        });

        var product = await catalogDb.RewardProducts.FirstAsync();
        var inventory = await catalogDb.RewardInventories.FirstAsync();

        Assert.Equal("Gift Card Updated", product.Name);
        Assert.Equal(120, product.PointsCost);
        Assert.Equal(5, inventory.AvailableQuantity);

        var list = await service.ListAsync();
        Assert.Single(list);
    }

    [Fact]
    public async Task SearchAsync_FiltersByTerm()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var catalogDb = TestDbContextFactory.CreateRewardCatalog(db.ConnectionString);
        await TestDbContextFactory.EnsureRewardCatalogSchemaAsync(catalogDb);

        var service = new RewardCatalogService(catalogDb);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        await service.UpsertAsync(new[]
        {
            new RewardProductUpsertRequest
            {
                TenantId = tenantId,
                RewardVendor = "VendorA",
                Sku = "SKU-1",
                Name = "Gift Card",
                PointsCost = 100
            },
            new RewardProductUpsertRequest
            {
                TenantId = tenantId,
                RewardVendor = "VendorB",
                Sku = "SKU-2",
                Name = "Promo Pack",
                PointsCost = 200
            },
            new RewardProductUpsertRequest
            {
                TenantId = otherTenantId,
                RewardVendor = "VendorB",
                Sku = "SKU-9",
                Name = "Promo Pack Other",
                PointsCost = 50
            }
        });

        var byVendor = await service.SearchAsync("VendorB", tenantId);
        Assert.Single(byVendor);

        var empty = await service.SearchAsync(" ");
        Assert.Empty(empty);
    }

    [Fact]
    public async Task ListAsync_FiltersByTenant()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var catalogDb = TestDbContextFactory.CreateRewardCatalog(db.ConnectionString);
        await TestDbContextFactory.EnsureRewardCatalogSchemaAsync(catalogDb);

        var service = new RewardCatalogService(catalogDb);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        await service.UpsertAsync(new[]
        {
            new RewardProductUpsertRequest
            {
                TenantId = tenantId,
                RewardVendor = "VendorA",
                Sku = "SKU-1",
                Name = "Gift Card",
                PointsCost = 100
            },
            new RewardProductUpsertRequest
            {
                TenantId = otherTenantId,
                RewardVendor = "VendorA",
                Sku = "SKU-2",
                Name = "Gift Card Other",
                PointsCost = 80
            }
        });

        var byTenant = await service.ListAsync(tenantId);
        Assert.Single(byTenant);
    }

    [Fact]
    public async Task ReserveAndRelease_UpdatesInventory()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var catalogDb = TestDbContextFactory.CreateRewardCatalog(db.ConnectionString);
        await TestDbContextFactory.EnsureRewardCatalogSchemaAsync(catalogDb);

        var service = new RewardCatalogService(catalogDb);
        var productId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        catalogDb.RewardProducts.Add(new RewardProduct
        {
            Id = productId,
            TenantId = tenantId,
            RewardVendor = "VendorA",
            Sku = "SKU-1",
            Name = "Gift Card",
            PointsCost = 100
        });
        catalogDb.RewardInventories.Add(new RewardInventory
        {
            RewardProductId = productId,
            AvailableQuantity = 10
        });
        await catalogDb.SaveChangesAsync();

        await service.ReserveAsync(tenantId, productId, 3);
        await service.ReleaseAsync(tenantId, productId, 0);
        await service.ReleaseAsync(tenantId, productId, 2);

        catalogDb.ChangeTracker.Clear();
        var inventory = await catalogDb.RewardInventories.AsNoTracking().FirstAsync();
        Assert.Equal(9, inventory.AvailableQuantity);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReserveAsync(tenantId, productId, 20));
        Assert.Contains("Insufficient inventory", ex.Message);

        var exQty = await Assert.ThrowsAsync<ArgumentException>(() => service.ReserveAsync(tenantId, productId, 0));
        Assert.Contains("Quantity must be greater than 0", exQty.Message);
    }

    [Fact]
    public async Task UpsertAsync_ValidatesInputs()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var catalogDb = TestDbContextFactory.CreateRewardCatalog(db.ConnectionString);
        await TestDbContextFactory.EnsureRewardCatalogSchemaAsync(catalogDb);

        var service = new RewardCatalogService(catalogDb);
        var tenantId = Guid.NewGuid();

        var exTenant = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new RewardProductUpsertRequest
                {
                    TenantId = Guid.Empty,
                    RewardVendor = "Vendor",
                    Sku = "SKU",
                    Name = "Name",
                    PointsCost = 1
                }
            }));
        Assert.Contains("TenantId is required", exTenant.Message);

        var exVendor = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new RewardProductUpsertRequest
                {
                    TenantId = tenantId,
                    RewardVendor = " ",
                    Sku = "SKU",
                    Name = "Name",
                    PointsCost = 10
                }
            }));
        Assert.Contains("RewardVendor is required", exVendor.Message);

        var exSku = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new RewardProductUpsertRequest
                {
                    TenantId = tenantId,
                    RewardVendor = "Vendor",
                    Sku = " ",
                    Name = "Name",
                    PointsCost = 10
                }
            }));
        Assert.Contains("Sku is required", exSku.Message);

        var exName = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new RewardProductUpsertRequest
                {
                    TenantId = tenantId,
                    RewardVendor = "Vendor",
                    Sku = "SKU",
                    Name = " ",
                    PointsCost = 10
                }
            }));
        Assert.Contains("Name is required", exName.Message);

        var exPoints = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new RewardProductUpsertRequest
                {
                    TenantId = tenantId,
                    RewardVendor = "Vendor",
                    Sku = "SKU",
                    Name = "Name",
                    PointsCost = -1
                }
            }));
        Assert.Contains("PointsCost cannot be negative", exPoints.Message);

        var exInventory = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new RewardProductUpsertRequest
                {
                    TenantId = tenantId,
                    RewardVendor = "Vendor",
                    Sku = "SKU",
                    Name = "Name",
                    PointsCost = 1,
                    InventoryQuantity = -5
                }
            }));
        Assert.Contains("InventoryQuantity cannot be negative", exInventory.Message);
    }
}
