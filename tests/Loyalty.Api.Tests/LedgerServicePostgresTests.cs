using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class LedgerServicePostgresTests
{
    [Fact]
    public async Task RedeemAndAdjust_RelationalPath()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);
        await using var ledgerDb = TestDbContextFactory.CreateLedger(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);
        await TestDbContextFactory.EnsureLedgerSchemaAsync(ledgerDb);

        var customer = new Customer { TenantId = Guid.NewGuid(), Name = "Outlet" };
        customersDb.Customers.Add(customer);
        await customersDb.SaveChangesAsync();

        var users = new UserService(customersDb);
        var user = await users.CreateAsync(new CreateUserCommand(customer.TenantId, customer.Id, "user@test.com", null, null));

        ledgerDb.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 50 });
        await ledgerDb.SaveChangesAsync();

        var ledger = new LedgerService(ledgerDb, users);
        await ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, user.Id, 20, PointsReasons.RewardRedeem, "corr-3"));
        ledgerDb.ChangeTracker.Clear();
        var account = await ledgerDb.PointsAccounts.AsNoTracking().FirstAsync(a => a.CustomerId == customer.Id);
        Assert.Equal(30, account.Balance);

        await ledger.AdjustAsync(new ManualAdjustPointsCommand(customer.Id, user.Id, 10, PointsReasons.ManualAdjustment, null));
        ledgerDb.ChangeTracker.Clear();
        var adjusted = await ledgerDb.PointsAccounts.AsNoTracking().FirstAsync(a => a.CustomerId == customer.Id);
        Assert.Equal(40, adjusted.Balance);

        var exInsufficient = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, user.Id, 100, PointsReasons.RewardRedeem, null)));
        Assert.Contains("Insufficient points", exInsufficient.Message);
    }
}
