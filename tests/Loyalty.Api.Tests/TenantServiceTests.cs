using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class TenantServiceTests
{
    private static TenantsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TenantsDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ValidatesName()
    {
        await using var db = CreateContext();
        var service = new TenantService(db);

        var ex = await Assert.ThrowsAsync<Exception>(() => service.CreateAsync("  "));
        Assert.Contains("Tenant name is required", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ListAndExists()
    {
        await using var db = CreateContext();
        var service = new TenantService(db);

        var a = await service.CreateAsync("B Tenant");
        var b = await service.CreateAsync("A Tenant");

        Assert.True(await service.ExistsAsync(a.Id));
        Assert.False(await service.ExistsAsync(Guid.NewGuid()));

        var list = await service.ListAsync();
        Assert.Equal(2, list.Count);
        Assert.Equal("A Tenant", list[0].Name);
        Assert.Equal("B Tenant", list[1].Name);
    }
}
