using System;
using System.Linq;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class CustomerServicePostgresTests
{
    [Fact]
    public async Task CreateAsync_UsesRelationalTransactionAndSeedsAccount()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);
        await using var ledgerDb = TestDbContextFactory.CreateLedger(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);
        await TestDbContextFactory.EnsureLedgerSchemaAsync(ledgerDb);

        var customers = new CustomerService(customersDb, ledgerDb);

        var tenant = new Loyalty.Api.Modules.Tenants.Domain.Tenant { Name = "Tenant A" };
        customersDb.Tenants.Add(tenant);
        await customersDb.SaveChangesAsync();

        var customer = await customers.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", "contact@test.com", "EXT-1"));

        var account = await ledgerDb.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == customer.Id);
        Assert.NotNull(account);
        Assert.Equal(0, account!.Balance);
    }

    [Fact]
    public async Task SearchByTenantAsync_FiltersByTerm()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);
        await using var ledgerDb = TestDbContextFactory.CreateLedger(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);
        await TestDbContextFactory.EnsureLedgerSchemaAsync(ledgerDb);

        var customers = new CustomerService(customersDb, ledgerDb);

        var tenant = new Loyalty.Api.Modules.Tenants.Domain.Tenant { Name = "Tenant Search" };
        customersDb.Tenants.Add(tenant);
        await customersDb.SaveChangesAsync();
        var a = await customers.CreateAsync(new CreateCustomerCommand(tenant.Id, "North Taproom", "north@test.com", "N-1"));
        var b = await customers.CreateAsync(new CreateCustomerCommand(tenant.Id, "South Tavern", "south@test.com", "S-1"));

        var empty = await customers.SearchByTenantAsync(tenant.Id, " ");
        Assert.Empty(empty);

        var result = await customers.SearchByTenantAsync(tenant.Id, "North");
        Assert.Single(result);
        Assert.Equal(a.Id, result[0].Id);

        var byExternal = await customers.SearchByTenantAsync(tenant.Id, "S-1");
        Assert.Single(byExternal);
        Assert.Equal(b.Id, byExternal[0].Id);
    }

    [Fact]
    public async Task GetAndList_ReturnsPointsAccount()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);
        await using var ledgerDb = TestDbContextFactory.CreateLedger(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);
        await TestDbContextFactory.EnsureLedgerSchemaAsync(ledgerDb);

        var customers = new CustomerService(customersDb, ledgerDb);

        var tenant = new Loyalty.Api.Modules.Tenants.Domain.Tenant { Name = "Tenant Points" };
        customersDb.Tenants.Add(tenant);
        await customersDb.SaveChangesAsync();
        var customer = await customers.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", null, null));

        var fetched = await customers.GetAsync(customer.Id);
        Assert.NotNull(fetched);
        Assert.NotNull(fetched!.PointsAccount);

        var list = await customers.ListByTenantAsync(tenant.Id);
        Assert.Single(list);
        Assert.NotNull(list[0].PointsAccount);
    }
}
