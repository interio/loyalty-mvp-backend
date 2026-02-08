using System;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class LedgerServiceInMemoryTests
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
    public async Task RedeemAsync_ValidatesInputs()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var users = new UserService(cd);
        var ledger = new LedgerService(ld, users);

        var exAmount = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(Guid.NewGuid(), Guid.NewGuid(), 0, PointsReasons.RewardRedeem, null)));
        Assert.Contains("Amount must be greater than 0", exAmount.Message);

        var exReason = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(Guid.NewGuid(), Guid.NewGuid(), 10, " ", null)));
        Assert.Contains("Reason is required", exReason.Message);

        var exUnknown = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(Guid.NewGuid(), Guid.NewGuid(), 10, "unknown", null)));
        Assert.Contains("Unknown reason", exUnknown.Message);
    }

    [Fact]
    public async Task RedeemAsync_ValidatesActorAndAccount()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var users = new UserService(cd);
        var ledger = new LedgerService(ld, users);

        var customer = new Customer { TenantId = Guid.NewGuid(), Name = "Outlet" };
        cd.Customers.Add(customer);
        await cd.SaveChangesAsync();

        var exActor = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, Guid.NewGuid(), 10, PointsReasons.RewardRedeem, null)));
        Assert.Contains("Actor user not found", exActor.Message);

        var user = await users.CreateAsync(new CreateUserCommand(customer.TenantId, customer.Id, "user@test.com", null, null));

        var otherCustomer = new Customer { TenantId = customer.TenantId, Name = "Other" };
        cd.Customers.Add(otherCustomer);
        await cd.SaveChangesAsync();

        var otherUser = await users.CreateAsync(new CreateUserCommand(customer.TenantId, otherCustomer.Id, "other@test.com", null, null));

        var exMismatch = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, otherUser.Id, 10, PointsReasons.RewardRedeem, null)));
        Assert.Contains("does not belong", exMismatch.Message);

        var exAccount = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, user.Id, 10, PointsReasons.RewardRedeem, null)));
        Assert.Contains("no points account", exAccount.Message);
    }

    [Fact]
    public async Task RedeemAsync_NonRelational_IdempotentAndInsufficient()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var users = new UserService(cd);
        var ledger = new LedgerService(ld, users);

        var customer = new Customer { TenantId = Guid.NewGuid(), Name = "Outlet" };
        cd.Customers.Add(customer);
        await cd.SaveChangesAsync();
        var user = await users.CreateAsync(new CreateUserCommand(customer.TenantId, customer.Id, "user@test.com", null, null));

        ld.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 5 });
        await ld.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Exception>(() =>
            ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, user.Id, 10, PointsReasons.RewardRedeem, null)));
        Assert.Contains("Insufficient points", ex.Message);

        var account = await ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, user.Id, 5, PointsReasons.RewardRedeem, "corr-1"));
        Assert.Equal(0, account.Balance);

        var idempotent = await ledger.RedeemAsync(new RedeemPointsCommand(customer.Id, user.Id, 5, PointsReasons.RewardRedeem, "corr-1"));
        Assert.Equal(0, idempotent.Balance);
    }

    [Fact]
    public async Task AdjustAsync_NonRelational_ValidationsAndIdempotency()
    {
        var (customersDb, ledgerDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        var users = new UserService(cd);
        var ledger = new LedgerService(ld, users);

        var customer = new Customer { TenantId = Guid.NewGuid(), Name = "Outlet" };
        cd.Customers.Add(customer);
        await cd.SaveChangesAsync();

        var exNoAccount = await Assert.ThrowsAsync<Exception>(() =>
            ledger.AdjustAsync(new ManualAdjustPointsCommand(
                customer.Id,
                null,
                null,
                null,
                10,
                PointsReasons.ManualAdjustment,
                null)));
        Assert.Contains("Customer has no points account", exNoAccount.Message);

        ld.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ld.SaveChangesAsync();

        var exAmount = await Assert.ThrowsAsync<Exception>(() =>
            ledger.AdjustAsync(new ManualAdjustPointsCommand(
                customer.Id,
                null,
                null,
                null,
                0,
                PointsReasons.ManualAdjustment,
                null)));
        Assert.Contains("Amount cannot be zero", exAmount.Message);

        var exReason = await Assert.ThrowsAsync<Exception>(() =>
            ledger.AdjustAsync(new ManualAdjustPointsCommand(
                customer.Id,
                null,
                null,
                null,
                10,
                PointsReasons.RewardRedeem,
                null)));
        Assert.Contains("Manual adjustments must use reason", exReason.Message);

        var account = await ledger.AdjustAsync(new ManualAdjustPointsCommand(
            customer.Id,
            null,
            null,
            null,
            10,
            PointsReasons.ManualAdjustment,
            "corr-2"));
        Assert.Equal(10, account.Balance);

        var idempotent = await ledger.AdjustAsync(new ManualAdjustPointsCommand(
            customer.Id,
            null,
            null,
            null,
            10,
            PointsReasons.ManualAdjustment,
            "corr-2"));
        Assert.Equal(10, idempotent.Balance);
    }
}
