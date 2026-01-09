using System;
using System.Linq;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.Tenants.Domain;
using Loyalty.Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class UserServicePostgresTests
{
    [Fact]
    public async Task CreateAsync_ValidatesInputsAndDuplicates()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);

        var users = new UserService(customersDb);

        var tenant = new Tenant { Name = "Tenant Users" };
        customersDb.Tenants.Add(tenant);
        await customersDb.SaveChangesAsync();
        var customer = new Customer { TenantId = tenant.Id, Name = "Outlet" };
        customersDb.Customers.Add(customer);
        await customersDb.SaveChangesAsync();

        var exEmail = await Assert.ThrowsAsync<Exception>(() =>
            users.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, " ", null, null)));
        Assert.Contains("Email is required", exEmail.Message);

        var exCustomer = await Assert.ThrowsAsync<Exception>(() =>
            users.CreateAsync(new CreateUserCommand(tenant.Id, Guid.NewGuid(), "a@test.com", null, null)));
        Assert.Contains("Customer not found", exCustomer.Message);

        var otherTenant = new Tenant { Name = "Other" };
        customersDb.Tenants.Add(otherTenant);
        await customersDb.SaveChangesAsync();
        var exTenant = await Assert.ThrowsAsync<Exception>(() =>
            users.CreateAsync(new CreateUserCommand(otherTenant.Id, customer.Id, "a@test.com", null, null)));
        Assert.Contains("Customer does not belong", exTenant.Message);

        var created = await users.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "a@test.com", "owner", "EXT-1"));
        Assert.Equal("a@test.com", created.Email);

        var exDupEmail = await Assert.ThrowsAsync<Exception>(() =>
            users.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "a@test.com", null, null)));
        Assert.Contains("Email already exists", exDupEmail.Message);

        var exDupExt = await Assert.ThrowsAsync<Exception>(() =>
            users.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "b@test.com", null, "EXT-1")));
        Assert.Contains("ExternalId already exists", exDupExt.Message);
    }

    [Fact]
    public async Task ListAndSearch_ReturnsMatches()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);

        var users = new UserService(customersDb);

        var tenant = new Tenant { Name = "Tenant Users" };
        customersDb.Tenants.Add(tenant);
        await customersDb.SaveChangesAsync();
        var customer = new Customer { TenantId = tenant.Id, Name = "Outlet" };
        customersDb.Customers.Add(customer);
        await customersDb.SaveChangesAsync();

        await users.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "alpha@test.com", "admin", "ALPHA"));
        await users.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "bravo@test.com", "owner", "BRAVO"));

        var empty = await users.SearchByTenantAsync(tenant.Id, " ");
        Assert.Empty(empty);

        var search = await users.SearchByTenantAsync(tenant.Id, "admin");
        Assert.Single(search);
        Assert.Equal("alpha@test.com", search[0].Email);

        var byCustomer = await users.ListByCustomerAsync(customer.Id);
        Assert.Equal(2, byCustomer.Count);
        Assert.All(byCustomer, u => Assert.NotNull(u.Customer));

        var byTenant = await users.ListByTenantAsync(tenant.Id);
        Assert.Equal(2, byTenant.Count);

        var fetched = await users.GetAsync(byTenant[0].Id);
        Assert.NotNull(fetched);
    }
}
