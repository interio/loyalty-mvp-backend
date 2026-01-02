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
    public async Task ApplyInvoice_IsIdempotent()
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

        var first = await service.ApplyInvoiceAsync(request);
        var second = await service.ApplyInvoiceAsync(request);

        Assert.False(first.WasDuplicate);
        Assert.True(second.WasDuplicate);
        Assert.Equal(first.NewBalance, second.NewBalance);
    }

    [Fact]
    public async Task ApplyInvoice_AllowsSameInvoiceIdAcrossTenants()
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

        var resultA = await service.ApplyInvoiceAsync(requestA);
        var resultB = await service.ApplyInvoiceAsync(requestB);

        Assert.False(resultA.WasDuplicate);
        Assert.False(resultB.WasDuplicate);
        Assert.Equal(20, resultA.PointsAwarded); // 200m spend => 20 points
        Assert.Equal(20, resultB.PointsAwarded);
    }
}
