using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class DistributorServiceTests
{
    [Fact]
    public async Task CreateAndSearch_WorkForTenantScope()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ProductsDbContext(options);
        var service = new DistributorService(db);
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant A" });
        await db.SaveChangesAsync();

        var distributor = await service.CreateAsync(new CreateDistributorCommand(
            tenantId,
            "heineken-dist",
            "Heineken Distributor"));

        Assert.Equal(tenantId, distributor.TenantId);
        Assert.Equal("heineken-dist", distributor.Name);

        var list = await service.ListByTenantAsync(tenantId);
        Assert.Single(list);

        var search = await service.SearchByTenantAsync(tenantId, "heineken");
        Assert.Single(search);
    }

    [Fact]
    public async Task Create_ValidatesTenantAndUniqueness()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ProductsDbContext(options);
        var service = new DistributorService(db);
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant A" });
        await db.SaveChangesAsync();

        var exTenant = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateDistributorCommand(Guid.NewGuid(), "dist", "Dist")));
        Assert.Contains("Tenant not found", exTenant.Message);

        await service.CreateAsync(new CreateDistributorCommand(tenantId, "dist", "Dist"));

        var exDup = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateDistributorCommand(tenantId, "dist", "Dist 2")));
        Assert.Contains("already exists", exDup.Message);
    }
}
