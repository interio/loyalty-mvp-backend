using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Domain;
using Loyalty.Api.Tests.TestHelpers;
using Xunit;

namespace Loyalty.Api.Tests;

public class DistributorServicePostgresTests
{
    [Fact]
    public async Task ListPageAsync_FiltersByTenantAndSearch()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var productsDb = TestDbContextFactory.CreateProducts(db.ConnectionString);
        await TestDbContextFactory.EnsureProductsSchemaAsync(productsDb);

        var service = new DistributorService(productsDb);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        productsDb.Tenants.AddRange(
            new Tenant { Id = tenantId, Name = "Tenant A" },
            new Tenant { Id = otherTenantId, Name = "Tenant B" });
        await productsDb.SaveChangesAsync();

        await service.CreateAsync(new CreateDistributorCommand(tenantId, "dist-1", "Heineken D1"));
        await service.CreateAsync(new CreateDistributorCommand(tenantId, "dist-2", "Cider D2"));
        await service.CreateAsync(new CreateDistributorCommand(otherTenantId, "dist-3", "Other Tenant"));

        var page = await service.ListByTenantPageAsync(tenantId, page: 1, pageSize: 10, search: "Heineken");
        Assert.Single(page.Items);
        Assert.Equal("dist-1", page.Items[0].Name);

        var allTenantA = await service.ListByTenantAsync(tenantId);
        Assert.Equal(2, allTenantA.Count);
    }
}
