using System;
using System.Threading.Tasks;
using Loyalty.Api.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Tenants.Application;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class CustomerAndLedgerTests
{
    private static LoyaltyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LoyaltyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LoyaltyDbContext(options);
    }

    [Fact]
    public async Task CreateCustomer_SeedsPointsAccount()
    {
        using var db = CreateDbContext();
        var tenants = new TenantService(db);
        var customers = new CustomerService(db);

        var tenant = await tenants.CreateAsync("Test Tenant");
        var customer = await customers.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", null, null));

        var account = await db.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == customer.Id);
        Assert.NotNull(account);
        Assert.Equal(0, account!.Balance);
    }

    [Fact]
    public async Task RedeemPoints_ThrowsWhenInsufficientBalance()
    {
        using var db = CreateDbContext();
        var tenants = new TenantService(db);
        var customers = new CustomerService(db);
        var users = new UserService(db);
        var ledger = new LedgerService(db, users);

        var tenant = await tenants.CreateAsync("Test Tenant");
        var customer = await customers.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", null, null));
        var user = await users.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "a@test.com", null, null));

        var ex = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, user.Id, 10, PointsReasons.RewardRedeem, null)));

        Assert.Contains("Insufficient points", ex.Message);
    }
}
