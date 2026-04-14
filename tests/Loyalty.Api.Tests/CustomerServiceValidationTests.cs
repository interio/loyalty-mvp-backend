using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class CustomerServiceValidationTests
{
    private static (CustomersDbContext customers, LedgerDbContext ledger) CreateContexts()
    {
        var dbName = Guid.NewGuid().ToString();
        var customersOptions = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ledgerOptions = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new CustomersDbContext(customersOptions), new LedgerDbContext(ledgerOptions));
    }

    [Fact]
    public async Task CreateAsync_ValidatesTenantAndName()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var service = new CustomerService(cd, ld);

        var exTenant = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateAsync(new CreateCustomerCommand(Guid.NewGuid(), "Outlet", null, null)));
        Assert.Contains("Tenant not found", exTenant.Message);

        var tenantId = Guid.NewGuid();
        cd.Tenants.Add(new Loyalty.Api.Modules.Tenants.Domain.Tenant { Id = tenantId, Name = "Tenant" });
        await cd.SaveChangesAsync();

        var exName = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateCustomerCommand(tenantId, " ", null, null)));
        Assert.Contains("Customer name is required", exName.Message);
    }

    [Fact]
    public async Task CreateAsync_ValidatesAndDefaultsTier()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var service = new CustomerService(cd, ld);

        var tenantId = Guid.NewGuid();
        cd.Tenants.Add(new Loyalty.Api.Modules.Tenants.Domain.Tenant { Id = tenantId, Name = "Tenant" });
        await cd.SaveChangesAsync();

        var withDefaultTier = await service.CreateAsync(new CreateCustomerCommand(tenantId, "Outlet A", null, null));
        Assert.Equal(CustomerTierCatalog.Bronze, withDefaultTier.Tier);

        var withExplicitTier = await service.CreateAsync(new CreateCustomerCommand(tenantId, "Outlet B", null, null, " GOLD "));
        Assert.Equal(CustomerTierCatalog.Gold, withExplicitTier.Tier);

        var exTier = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(new CreateCustomerCommand(tenantId, "Outlet C", null, null, "diamond")));
        Assert.Contains("Customer tier must be one of", exTier.Message);
    }

    [Fact]
    public async Task UpdateTierAsync_UpdatesTier_AndValidatesScope()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var service = new CustomerService(cd, ld);

        var tenantId = Guid.NewGuid();
        cd.Tenants.Add(new Loyalty.Api.Modules.Tenants.Domain.Tenant { Id = tenantId, Name = "Tenant" });
        await cd.SaveChangesAsync();

        var customer = await service.CreateAsync(new CreateCustomerCommand(tenantId, "Outlet", null, "EXT-1"));
        Assert.Equal(CustomerTierCatalog.Bronze, customer.Tier);

        var updated = await service.UpdateTierAsync(customer.Id, tenantId, "Platinum");
        Assert.Equal(CustomerTierCatalog.Platinum, updated.Tier);

        var wrongTenant = await Assert.ThrowsAsync<Exception>(() =>
            service.UpdateTierAsync(customer.Id, Guid.NewGuid(), "gold"));
        Assert.Contains("Customer not found for tenant", wrongTenant.Message);

        var invalidTier = await Assert.ThrowsAsync<Exception>(() =>
            service.UpdateTierAsync(customer.Id, tenantId, "diamond"));
        Assert.Contains("Customer tier must be one of", invalidTier.Message);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExtendedProfileFields()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var service = new CustomerService(cd, ld);

        var tenantId = Guid.NewGuid();
        cd.Tenants.Add(new Loyalty.Api.Modules.Tenants.Domain.Tenant { Id = tenantId, Name = "Tenant" });
        await cd.SaveChangesAsync();

        var created = await service.CreateAsync(new CreateCustomerCommand(tenantId, "Outlet", null, "EXT-1"));

        var onboardDate = new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero);
        var updated = await service.UpdateAsync(new UpdateCustomerCommand(
            created.Id,
            tenantId,
            "Updated Outlet",
            "ops@updated.test",
            "EXT-ERP-1",
            "gold",
            new CustomerAddress
            {
                Address = "Main Street 12",
                CountryCode = "pl",
                PostalCode = "00-950",
                Region = "Mazowieckie"
            },
            "+48 555 777 111",
            "restaurant",
            "on-trade",
            onboardDate,
            CustomerStatusCatalog.Active));

        Assert.Equal("Updated Outlet", updated.Name);
        Assert.Equal("ops@updated.test", updated.ContactEmail);
        Assert.Equal("EXT-ERP-1", updated.ExternalId);
        Assert.Equal(CustomerTierCatalog.Gold, updated.Tier);
        Assert.NotNull(updated.Address);
        Assert.Equal("PL", updated.Address!.CountryCode);
        Assert.Equal("00-950", updated.Address.PostalCode);
        Assert.Equal("+48 555 777 111", updated.PhoneNumber);
        Assert.Equal("restaurant", updated.Type);
        Assert.Equal("on-trade", updated.BusinessSegment);
        Assert.Equal(onboardDate, updated.OnboardDate);
        Assert.Equal(CustomerStatusCatalog.Active, updated.Status);
    }

    [Fact]
    public async Task UpdateAsync_ValidatesStatusAndScope()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var service = new CustomerService(cd, ld);

        var tenantId = Guid.NewGuid();
        cd.Tenants.Add(new Loyalty.Api.Modules.Tenants.Domain.Tenant { Id = tenantId, Name = "Tenant" });
        await cd.SaveChangesAsync();

        var created = await service.CreateAsync(new CreateCustomerCommand(tenantId, "Outlet", null, "EXT-1"));

        var invalidStatus = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateAsync(new UpdateCustomerCommand(
                created.Id,
                tenantId,
                "Outlet Updated",
                null,
                "EXT-1",
                null,
                null,
                null,
                null,
                null,
                null,
                9)));
        Assert.Contains("Customer status must be one of", invalidStatus.Message);

        var wrongTenant = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync(new UpdateCustomerCommand(
                created.Id,
                Guid.NewGuid(),
                "Outlet Updated",
                null,
                "EXT-1")));
        Assert.Contains("Customer not found for tenant", wrongTenant.Message);
    }
}
