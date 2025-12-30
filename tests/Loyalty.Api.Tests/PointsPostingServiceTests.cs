using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loyalty.Api.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.Integration.Application;
using Loyalty.Api.Modules.Integration.Application.Invoices;
using Loyalty.Api.Modules.Integration.Application.Rules;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsPostingServiceTests
{
    private (LoyaltyDbContext loyalty, IntegrationDbContext integration) CreateContexts(string dbName)
    {
        var loyaltyOptions = new DbContextOptionsBuilder<LoyaltyDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var integrationOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new LoyaltyDbContext(loyaltyOptions), new IntegrationDbContext(integrationOptions));
    }

    [Fact]
    public async Task ApplyInvoice_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var (loyalty, integration) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        var customer = new Customer { TenantId = tenantId, Name = "Cust", ExternalId = "CUST-1" };
        loyalty.Customers.Add(customer);
        loyalty.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await loyalty.SaveChangesAsync();

        var service = new PointsPostingService(loyalty, integration, new HardcodedRulesProvider());

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
