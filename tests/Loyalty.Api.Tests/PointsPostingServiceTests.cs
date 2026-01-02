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
}
