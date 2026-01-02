using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class CustomerAndLedgerTests
{
    private static (TenantsDbContext tenantsDb, CustomersDbContext customersDb, LedgerDbContext ledgerDb) CreateContexts()
    {
        var dbName = Guid.NewGuid().ToString();

        var tenantsOptions = new DbContextOptionsBuilder<TenantsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var customersOptions = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ledgerOptions = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new TenantsDbContext(tenantsOptions), new CustomersDbContext(customersOptions), new LedgerDbContext(ledgerOptions));
    }

    [Fact]
    public async Task CreateCustomer_SeedsPointsAccount()
    {
        var (tenantsDb, customersDb, ledgerDb) = CreateContexts();
        using var td = tenantsDb;
        using var cd = customersDb;
        using var ld = ledgerDb;

        var tenants = new TenantService(td);
        var customers = new CustomerService(cd, ld);

        var tenant = await tenants.CreateAsync("Test Tenant");
        var customer = await customers.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", null, null));

        var account = await ld.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == customer.Id);
        Assert.NotNull(account);
        Assert.Equal(0, account!.Balance);
    }

    [Fact]
    public async Task RedeemPoints_ThrowsWhenInsufficientBalance()
    {
        var (tenantsDb, customersDb, ledgerDb) = CreateContexts();
        using var td = tenantsDb;
        using var cd = customersDb;
        using var ld = ledgerDb;

        var tenants = new TenantService(td);
        var customers = new CustomerService(cd, ld);
        var users = new UserService(cd);
        var ledger = new LedgerService(ld, users);

        var tenant = await tenants.CreateAsync("Test Tenant");
        var customer = await customers.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", null, null));
        var user = await users.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "a@test.com", null, null));

        var ex = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, user.Id, 10, PointsReasons.RewardRedeem, null)));

        Assert.Contains("Insufficient points", ex.Message);
    }
}
