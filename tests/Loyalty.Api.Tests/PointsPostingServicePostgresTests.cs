using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Loyalty.Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsPostingServicePostgresTests
{
    [Fact]
    public async Task ProcessPendingInvoicesAsync_RelationalPathProcessesDocs()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var ledger = TestDbContextFactory.CreateLedger(db.ConnectionString);
        await using var customers = TestDbContextFactory.CreateCustomers(db.ConnectionString);
        await using var integration = TestDbContextFactory.CreateIntegration(db.ConnectionString);
        await using var products = TestDbContextFactory.CreateProducts(db.ConnectionString);

        await TestDbContextFactory.EnsureCustomersSchemaAsync(customers);
        await TestDbContextFactory.EnsureLedgerSchemaAsync(ledger);
        await TestDbContextFactory.EnsureIntegrationSchemaAsync(integration);
        await TestDbContextFactory.EnsureProductsSchemaAsync(products);

        var tenantId = Guid.NewGuid();
        var customer = new Customer { TenantId = tenantId, Name = "Outlet", ExternalId = "EXT-1" };
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        ledger.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        var request = new InvoiceUpsertRequest
        {
            TenantId = tenantId,
            InvoiceId = "INV-REL-1",
            CustomerExternalId = "EXT-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest> { new() { Sku = "A", Quantity = 1, NetAmount = 0m } }
        };

        var payload = JsonSerializer.SerializeToNode(request) as System.Text.Json.Nodes.JsonObject ?? new();
        integration.InboundDocuments.Add(new InboundDocument
        {
            TenantId = tenantId,
            ExternalId = request.InvoiceId,
            DocumentType = "invoice",
            Payload = payload,
            Status = InboundDocumentStatus.PendingPoints
        });
        await integration.SaveChangesAsync();

        var service = new PointsPostingService(ledger, customers, integration, products, new EmptyRuleProvider());
        await service.ProcessPendingInvoicesAsync(10);

        var doc = await integration.InboundDocuments.FirstAsync();
        Assert.Equal(InboundDocumentStatus.Processed, doc.Status);
    }

    private sealed class EmptyRuleProvider : IInvoicePointsRuleProvider
    {
        public Task<IReadOnlyList<IInvoicePointsRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IInvoicePointsRule>>(Array.Empty<IInvoicePointsRule>());
    }
}
