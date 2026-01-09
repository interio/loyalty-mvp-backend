using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Loyalty.Api.Modules.RewardOrders.Application;
using Loyalty.Api.Modules.RewardOrders.Domain;
using Loyalty.Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class RewardOrderServiceTests
{
    [Fact]
    public async Task PlaceOrderAsync_CreatesOrderAndRedeemsPoints()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);
        await using var ledgerDb = TestDbContextFactory.CreateLedger(db.ConnectionString);
        await using var catalogDb = TestDbContextFactory.CreateRewardCatalog(db.ConnectionString);
        await using var ordersDb = TestDbContextFactory.CreateRewardOrders(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);
        await TestDbContextFactory.EnsureLedgerSchemaAsync(ledgerDb);
        await TestDbContextFactory.EnsureRewardCatalogSchemaAsync(catalogDb);
        await TestDbContextFactory.EnsureRewardOrdersSchemaAsync(ordersDb);

        var tenant = new Loyalty.Api.Modules.Tenants.Domain.Tenant { Name = "Tenant Rewards" };
        customersDb.Tenants.Add(tenant);
        await customersDb.SaveChangesAsync();

        var customerService = new CustomerService(customersDb, ledgerDb);
        var customer = await customerService.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", null, "EXT"));

        var userService = new UserService(customersDb);
        var user = await userService.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "user@test.com", "owner", "U1"));

        var catalogService = new RewardCatalogService(catalogDb);
        await catalogService.UpsertAsync(new[]
        {
            new RewardProductUpsertRequest
            {
                TenantId = tenant.Id,
                RewardVendor = "VendorA",
                Sku = "SKU-1",
                Name = "Gift Card",
                PointsCost = 100,
                InventoryQuantity = 5
            }
        });

        var product = await catalogDb.RewardProducts.FirstAsync();

        var ledgerService = new LedgerService(ledgerDb, userService);
        await ledgerService.AdjustAsync(new ManualAdjustPointsCommand(
            customer.Id,
            null,
            200,
            PointsReasons.ManualAdjustment,
            "seed-balance"));
        var dispatcher = new StubRewardOrderDispatcher();
        var orderService = new RewardOrderService(
            ordersDb,
            catalogService,
            catalogService,
            ledgerService,
            customerService,
            dispatcher);

        var order = await orderService.PlaceOrderAsync(new PlaceRewardOrderRequest
        {
            TenantId = tenant.Id,
            CustomerId = customer.Id,
            ActorUserId = user.Id,
            Items = new() { new RewardOrderLineRequest(product.Id, 2) }
        }, false);

        Assert.Equal(RewardOrderStatus.PendingDispatch, order.Status);
        Assert.False(order.PlacedOnBehalf);
        Assert.Equal(200, order.TotalPoints);

        catalogDb.ChangeTracker.Clear();
        var inventory = await catalogDb.RewardInventories.AsNoTracking().FirstAsync();
        Assert.Equal(3, inventory.AvailableQuantity);

        ledgerDb.ChangeTracker.Clear();
        var account = await ledgerDb.PointsAccounts.AsNoTracking().FirstAsync(a => a.CustomerId == customer.Id);
        Assert.Equal(0, account.Balance);

        var tx = await ledgerDb.PointsTransactions
            .FirstAsync(t => t.Reason == PointsReasons.RewardRedeem);
        Assert.Equal(PointsReasons.RewardRedeem, tx.Reason);
        Assert.Equal(order.Id.ToString(), tx.CorrelationId);
    }

    [Fact]
    public async Task PlaceOrderAsync_OnBehalf_SetsFlag()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);
        await using var ledgerDb = TestDbContextFactory.CreateLedger(db.ConnectionString);
        await using var catalogDb = TestDbContextFactory.CreateRewardCatalog(db.ConnectionString);
        await using var ordersDb = TestDbContextFactory.CreateRewardOrders(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);
        await TestDbContextFactory.EnsureLedgerSchemaAsync(ledgerDb);
        await TestDbContextFactory.EnsureRewardCatalogSchemaAsync(catalogDb);
        await TestDbContextFactory.EnsureRewardOrdersSchemaAsync(ordersDb);

        var tenant = new Loyalty.Api.Modules.Tenants.Domain.Tenant { Name = "Tenant Rewards" };
        customersDb.Tenants.Add(tenant);
        await customersDb.SaveChangesAsync();

        var customerService = new CustomerService(customersDb, ledgerDb);
        var customer = await customerService.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", null, "EXT"));

        var userService = new UserService(customersDb);
        var admin = await userService.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "admin@test.com", "admin", "A1"));

        var catalogService = new RewardCatalogService(catalogDb);
        await catalogService.UpsertAsync(new[]
        {
            new RewardProductUpsertRequest
            {
                TenantId = tenant.Id,
                RewardVendor = "VendorA",
                Sku = "SKU-1",
                Name = "Gift Card",
                PointsCost = 50,
                InventoryQuantity = 2
            }
        });
        var product = await catalogDb.RewardProducts.FirstAsync();

        var ledgerService = new LedgerService(ledgerDb, userService);
        await ledgerService.AdjustAsync(new ManualAdjustPointsCommand(
            customer.Id,
            null,
            200,
            PointsReasons.ManualAdjustment,
            "seed-balance"));
        var orderService = new RewardOrderService(
            ordersDb,
            catalogService,
            catalogService,
            ledgerService,
            customerService,
            new StubRewardOrderDispatcher());

        var order = await orderService.PlaceOrderAsync(new PlaceRewardOrderRequest
        {
            TenantId = tenant.Id,
            CustomerId = customer.Id,
            ActorUserId = admin.Id,
            Items = new() { new RewardOrderLineRequest(product.Id, 1) }
        }, true);

        Assert.True(order.PlacedOnBehalf);
    }

    [Fact]
    public async Task PlaceOrderAsync_FailsAndReleasesInventory()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var customersDb = TestDbContextFactory.CreateCustomers(db.ConnectionString);
        await using var ledgerDb = TestDbContextFactory.CreateLedger(db.ConnectionString);
        await using var catalogDb = TestDbContextFactory.CreateRewardCatalog(db.ConnectionString);
        await using var ordersDb = TestDbContextFactory.CreateRewardOrders(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customersDb);
        await TestDbContextFactory.EnsureLedgerSchemaAsync(ledgerDb);
        await TestDbContextFactory.EnsureRewardCatalogSchemaAsync(catalogDb);
        await TestDbContextFactory.EnsureRewardOrdersSchemaAsync(ordersDb);

        var tenant = new Loyalty.Api.Modules.Tenants.Domain.Tenant { Name = "Tenant Rewards" };
        customersDb.Tenants.Add(tenant);
        await customersDb.SaveChangesAsync();

        var customerService = new CustomerService(customersDb, ledgerDb);
        var customer = await customerService.CreateAsync(new CreateCustomerCommand(tenant.Id, "Outlet", null, "EXT"));

        var userService = new UserService(customersDb);
        var user = await userService.CreateAsync(new CreateUserCommand(tenant.Id, customer.Id, "user@test.com", "owner", "U1"));

        var catalogService = new RewardCatalogService(catalogDb);
        await catalogService.UpsertAsync(new[]
        {
            new RewardProductUpsertRequest
            {
                TenantId = tenant.Id,
                RewardVendor = "VendorA",
                Sku = "SKU-1",
                Name = "Gift Card",
                PointsCost = 100,
                InventoryQuantity = 1
            }
        });
        var product = await catalogDb.RewardProducts.FirstAsync();

        var failingLedger = new FailingLedgerService();
        var orderService = new RewardOrderService(
            ordersDb,
            catalogService,
            catalogService,
            failingLedger,
            customerService,
            new StubRewardOrderDispatcher());

        await Assert.ThrowsAsync<Exception>(() =>
            orderService.PlaceOrderAsync(new PlaceRewardOrderRequest
            {
                TenantId = tenant.Id,
                CustomerId = customer.Id,
                ActorUserId = user.Id,
                Items = new() { new RewardOrderLineRequest(product.Id, 1) }
            }, false));

        var order = await ordersDb.RewardOrders.FirstAsync();
        Assert.Equal(RewardOrderStatus.Failed, order.Status);

        catalogDb.ChangeTracker.Clear();
        var inventory = await catalogDb.RewardInventories.AsNoTracking().FirstAsync();
        Assert.Equal(1, inventory.AvailableQuantity);
    }

    [Fact]
    public async Task ListByCustomerAndTenant_ReturnsOrders()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var ordersDb = TestDbContextFactory.CreateRewardOrders(db.ConnectionString);
        await TestDbContextFactory.EnsureRewardOrdersSchemaAsync(ordersDb);

        var order = new RewardOrder
        {
            TenantId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            Status = RewardOrderStatus.PendingDispatch,
            TotalPoints = 100,
            Items = { new RewardOrderItem
                {
                    RewardProductId = Guid.NewGuid(),
                    RewardVendor = "VendorA",
                    Sku = "SKU",
                    Name = "Gift",
                    Quantity = 1,
                    PointsCost = 100,
                    TotalPoints = 100
                }
            }
        };
        ordersDb.RewardOrders.Add(order);
        await ordersDb.SaveChangesAsync();

        var service = new RewardOrderService(
            ordersDb,
            new FakeCatalogLookup(order.Items.Select(i => i.RewardProductId).ToList()),
            new FakeInventoryService(),
            new FailingLedgerService(),
            new FakeCustomerLookup(true),
            new StubRewardOrderDispatcher());

        var byCustomer = await service.ListByCustomerAsync(order.CustomerId);
        var byTenant = await service.ListByTenantAsync(order.TenantId);

        Assert.Single(byCustomer);
        Assert.Single(byTenant);
    }

    [Fact]
    public async Task PlaceOrderAsync_ValidatesRequest()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var ordersDb = TestDbContextFactory.CreateRewardOrders(db.ConnectionString);
        await TestDbContextFactory.EnsureRewardOrdersSchemaAsync(ordersDb);

        var service = new RewardOrderService(
            ordersDb,
            new FakeCatalogLookup(new List<Guid>()),
            new FakeInventoryService(),
            new FailingLedgerService(),
            new FakeCustomerLookup(false),
            new StubRewardOrderDispatcher());

        var exTenant = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PlaceOrderAsync(new PlaceRewardOrderRequest
            {
                TenantId = Guid.Empty,
                CustomerId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                Items = new() { new RewardOrderLineRequest(Guid.NewGuid(), 1) }
            }, false));
        Assert.Contains("TenantId is required", exTenant.Message);

        var exItems = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PlaceOrderAsync(new PlaceRewardOrderRequest
            {
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                Items = new()
            }, false));
        Assert.Contains("At least one item", exItems.Message);

        var exQty = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PlaceOrderAsync(new PlaceRewardOrderRequest
            {
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                Items = new() { new RewardOrderLineRequest(Guid.NewGuid(), 0) }
            }, false));
        Assert.Contains("Quantity must be greater than 0", exQty.Message);

        var exBelongs = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PlaceOrderAsync(new PlaceRewardOrderRequest
            {
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                Items = new() { new RewardOrderLineRequest(Guid.NewGuid(), 1) }
            }, false));
        Assert.Contains("Customer does not belong", exBelongs.Message);

        var serviceMissingProduct = new RewardOrderService(
            ordersDb,
            new FakeCatalogLookup(new List<Guid>()),
            new FakeInventoryService(),
            new FailingLedgerService(),
            new FakeCustomerLookup(true),
            new StubRewardOrderDispatcher());

        var exProduct = await Assert.ThrowsAsync<ArgumentException>(() =>
            serviceMissingProduct.PlaceOrderAsync(new PlaceRewardOrderRequest
            {
                TenantId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                ActorUserId = Guid.NewGuid(),
                Items = new() { new RewardOrderLineRequest(Guid.NewGuid(), 1) }
            }, false));
        Assert.Contains("reward products not found", exProduct.Message);
    }

    private sealed class FailingLedgerService : ILedgerService
    {
        public Task<List<PointsTransaction>> GetTransactionsForCustomerAsync(Guid customerId, int take = 200, CancellationToken ct = default) =>
            Task.FromResult(new List<PointsTransaction>());

        public Task<PointsAccount> RedeemAsync(RedeemPointsCommand command, CancellationToken ct = default) =>
            throw new Exception("Ledger failure");

        public Task<PointsAccount> AdjustAsync(ManualAdjustPointsCommand command, CancellationToken ct = default) =>
            throw new Exception("Ledger failure");
    }

    private sealed class FakeCatalogLookup : IRewardCatalogLookup
    {
        private readonly List<Guid> _ids;

        public FakeCatalogLookup(List<Guid> ids) => _ids = ids;

        public Task<List<RewardProduct>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            var products = _ids
                .Where(id => ids.Contains(id))
                .Select(id => new RewardProduct
                {
                    Id = id,
                    RewardVendor = "VendorA",
                    Sku = "SKU",
                    Name = "Gift",
                    PointsCost = 100
                }).ToList();
            return Task.FromResult(products);
        }
    }

    private sealed class FakeInventoryService : IRewardInventoryService
    {
        public Task ReserveAsync(Guid rewardProductId, int quantity, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReleaseAsync(Guid rewardProductId, int quantity, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCustomerLookup : ICustomerLookup
    {
        private readonly bool _belongs;

        public FakeCustomerLookup(bool belongs) => _belongs = belongs;

        public Task<Loyalty.Api.Modules.Customers.Domain.Customer?> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Loyalty.Api.Modules.Customers.Domain.Customer?>(null);

        public Task<bool> BelongsToTenantAsync(Guid customerId, Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult(_belongs);
    }
}
