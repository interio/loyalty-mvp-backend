using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsPostingServiceTests
{
    private (LedgerDbContext ledger, CustomersDbContext customers, IntegrationDbContext integration) CreateContexts(string dbName)
    {
        var ledgerOptions = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var customersOptions = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var integrationOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new LedgerDbContext(ledgerOptions), new CustomersDbContext(customersOptions), new IntegrationDbContext(integrationOptions));
    }

    [Fact]
    public async Task ProcessPendingInvoices_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        var customer = new Customer { TenantId = tenantId, Name = "Cust", ExternalId = "CUST-1" };
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        ledger.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        var service = new PointsPostingService(ledger, customers, integration, new HardcodedRulesProvider());

        var request = new InvoiceUpsertRequest
        {
            TenantId = tenantId,
            InvoiceId = "INV-1",
            CustomerExternalId = "CUST-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "A", Quantity = 1, NetAmount = 200m }
            }
        };

        await service.IngestInvoiceAsync(request);
        await service.IngestInvoiceAsync(request); // duplicate ingest should be fine

        await service.ProcessPendingInvoicesAsync(10);
        await service.ProcessPendingInvoicesAsync(10); // re-run should not duplicate ledger

        var account = await ledger.PointsAccounts.FirstAsync(a => a.CustomerId == customer.Id);
        Assert.Equal(20, account.Balance);
        Assert.Equal(1, await ledger.PointsTransactions.CountAsync());
    }

    [Fact]
    public async Task ProcessPendingInvoices_AllowsSameInvoiceIdAcrossTenants()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration) = CreateContexts(dbName);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var customerA = new Customer { TenantId = tenantA, Name = "A", ExternalId = "CUST" };
        var customerB = new Customer { TenantId = tenantB, Name = "B", ExternalId = "CUST" };
        customers.Customers.AddRange(customerA, customerB);
        await customers.SaveChangesAsync();

        ledger.PointsAccounts.AddRange(
            new PointsAccount { CustomerId = customerA.Id, Balance = 0 },
            new PointsAccount { CustomerId = customerB.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        var service = new PointsPostingService(ledger, customers, integration, new HardcodedRulesProvider());

        var requestA = new InvoiceUpsertRequest
        {
            TenantId = tenantA,
            InvoiceId = "INV-1",
            CustomerExternalId = "CUST",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "A", Quantity = 1, NetAmount = 200m }
            }
        };

        var requestB = new InvoiceUpsertRequest
        {
            TenantId = tenantB,
            InvoiceId = "INV-1",
            CustomerExternalId = "CUST",
            OccurredAt = requestA.OccurredAt,
            Lines = requestA.Lines
        };

        await service.IngestInvoiceAsync(requestA);
        await service.IngestInvoiceAsync(requestB);
        await service.ProcessPendingInvoicesAsync(10);

        var accountA = await ledger.PointsAccounts.FirstAsync(a => a.CustomerId == customerA.Id);
        var accountB = await ledger.PointsAccounts.FirstAsync(a => a.CustomerId == customerB.Id);

        Assert.Equal(20, accountA.Balance); // 200m spend => 20 points
        Assert.Equal(20, accountB.Balance);
        Assert.Equal(2, await ledger.PointsTransactions.CountAsync());
    }
}
