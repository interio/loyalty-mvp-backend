using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
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

        var exTenant = await Assert.ThrowsAsync<Exception>(() =>
            service.CreateAsync(new CreateCustomerCommand(Guid.NewGuid(), "Outlet", null, null)));
        Assert.Contains("Tenant not found", exTenant.Message);

        var tenantId = Guid.NewGuid();
        cd.Tenants.Add(new Loyalty.Api.Modules.Tenants.Domain.Tenant { Id = tenantId, Name = "Tenant" });
        await cd.SaveChangesAsync();

        var exName = await Assert.ThrowsAsync<Exception>(() =>
            service.CreateAsync(new CreateCustomerCommand(tenantId, " ", null, null)));
        Assert.Contains("Customer name is required", exName.Message);
    }
}
