using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsPostingServiceTests
{
    private (LedgerDbContext ledger, CustomersDbContext customers, IntegrationDbContext integration, ProductsDbContext products) CreateContexts(string dbName)
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
        var productsOptions = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (new LedgerDbContext(ledgerOptions),
            new CustomersDbContext(customersOptions),
            new IntegrationDbContext(integrationOptions),
            new ProductsDbContext(productsOptions));
    }

    [Fact]
    public async Task ProcessPendingInvoices_IsIdempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        var customer = new Customer { TenantId = tenantId, Name = "Cust", ExternalId = "CUST-1" };
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        ledger.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        var service = new PointsPostingService(ledger, customers, integration, products, new HardcodedRulesProvider());

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
        var (ledger, customers, integration, products) = CreateContexts(dbName);

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

        var service = new PointsPostingService(ledger, customers, integration, products, new HardcodedRulesProvider());

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

    [Fact]
    public async Task IngestInvoiceAsync_UpdatesExistingDocument()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        var customer = new Customer { TenantId = tenantId, Name = "Cust", ExternalId = "CUST-1" };
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        ledger.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        var service = new PointsPostingService(ledger, customers, integration, products, new HardcodedRulesProvider());

        var request = new InvoiceUpsertRequest
        {
            TenantId = tenantId,
            InvoiceId = "INV-2",
            CustomerExternalId = "CUST-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "A", Quantity = 1, NetAmount = 200m }
            }
        };

        await service.IngestInvoiceAsync(request);
        await service.IngestInvoiceAsync(request);

        var doc = await integration.InboundDocuments.FirstAsync();
        Assert.Equal(InboundDocumentStatus.PendingPoints, doc.Status);
        Assert.Null(doc.Error);
    }

    [Fact]
    public async Task AwardInvoiceAsync_ResolvesActorUserId()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        var customer = new Customer { TenantId = tenantId, Name = "Cust", ExternalId = "CUST-1" };
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        ledger.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        var actor = new User
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            Email = "actor@test.com",
            ExternalId = "ACT-1"
        };
        customers.Users.Add(actor);
        await customers.SaveChangesAsync();

        var service = new PointsPostingService(ledger, customers, integration, products, new HardcodedRulesProvider());

        var request = new InvoiceUpsertRequest
        {
            TenantId = tenantId,
            InvoiceId = "INV-3",
            CustomerExternalId = "CUST-1",
            ActorExternalId = "ACT-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "BEER-HEINEKEN-BTL-24PK", Quantity = 4m, NetAmount = 0m }
            }
        };

        var response = await service.AwardInvoiceAsync(request);
        Assert.False(response.WasDuplicate);

        var tx = await ledger.PointsTransactions.FirstAsync();
        Assert.Equal(actor.Id, tx.ActorUserId);

        request.ActorExternalId = null;
        request.ActorEmail = "actor@test.com";
        request.InvoiceId = "INV-4";

        await service.AwardInvoiceAsync(request);
        var second = await ledger.PointsTransactions.OrderBy(t => t.CreatedAt).LastAsync();
        Assert.Equal(actor.Id, second.ActorUserId);
    }

    [Fact]
    public async Task AwardInvoiceAsync_ValidatesRequest()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);

        var service = new PointsPostingService(ledger, customers, integration, products, new HardcodedRulesProvider());

        var request = new InvoiceUpsertRequest
        {
            TenantId = Guid.Empty,
            InvoiceId = " ",
            CustomerExternalId = " ",
            OccurredAt = default,
            Lines = new List<InvoiceLineRequest>()
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.AwardInvoiceAsync(request));
        Assert.Contains("tenantId is required", ex.Message);

        request.TenantId = Guid.NewGuid();
        var exInvoice = await Assert.ThrowsAsync<ArgumentException>(() => service.AwardInvoiceAsync(request));
        Assert.Contains("invoiceId is required", exInvoice.Message);

        request.InvoiceId = "INV";
        var exOccurred = await Assert.ThrowsAsync<ArgumentException>(() => service.AwardInvoiceAsync(request));
        Assert.Contains("occurredAt is required", exOccurred.Message);

        request.OccurredAt = DateTimeOffset.UtcNow;
        var exCustomer = await Assert.ThrowsAsync<ArgumentException>(() => service.AwardInvoiceAsync(request));
        Assert.Contains("customerExternalId is required", exCustomer.Message);

        request.CustomerExternalId = "CUST";
        var exLines = await Assert.ThrowsAsync<ArgumentException>(() => service.AwardInvoiceAsync(request));
        Assert.Contains("lines are required", exLines.Message);

        request.Lines = new List<InvoiceLineRequest>
        {
            new() { Sku = " ", Quantity = 1, NetAmount = 1m }
        };
        var exSku = await Assert.ThrowsAsync<ArgumentException>(() => service.AwardInvoiceAsync(request));
        Assert.Contains("line.sku is required", exSku.Message);

        request.Lines = new List<InvoiceLineRequest>
        {
            new() { Sku = "SKU", Quantity = -1, NetAmount = 1m }
        };
        var exQty = await Assert.ThrowsAsync<ArgumentException>(() => service.AwardInvoiceAsync(request));
        Assert.Contains("line.quantity must be >= 0", exQty.Message);

        request.Lines = new List<InvoiceLineRequest>
        {
            new() { Sku = "SKU", Quantity = 1, NetAmount = -1m }
        };
        var exNet = await Assert.ThrowsAsync<ArgumentException>(() => service.AwardInvoiceAsync(request));
        Assert.Contains("line.netAmount must be >= 0", exNet.Message);
    }

    [Fact]
    public async Task AwardInvoiceAsync_ReturnsDuplicateWhenAlreadyPosted()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        var customer = new Customer { TenantId = tenantId, Name = "Cust", ExternalId = "CUST-1" };
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        ledger.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        ledger.PointsTransactions.Add(new PointsTransaction
        {
            CustomerId = customer.Id,
            Amount = 10,
            Reason = PointsReasons.InvoiceEarn,
            CorrelationId = "INV-dup"
        });
        await ledger.SaveChangesAsync();

        var service = new PointsPostingService(ledger, customers, integration, products, new HardcodedRulesProvider());
        var response = await service.AwardInvoiceAsync(new InvoiceUpsertRequest
        {
            TenantId = tenantId,
            InvoiceId = "INV-dup",
            CustomerExternalId = "CUST-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest> { new() { Sku = "A", Quantity = 1, NetAmount = 100m } }
        });

        Assert.True(response.WasDuplicate);
    }

    [Fact]
    public async Task ProcessPendingInvoicesAsync_MarksFailedOnError()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        integration.InboundDocuments.Add(new InboundDocument
        {
            TenantId = tenantId,
            ExternalId = "INV-bad",
            DocumentType = "invoice",
            Payload = new System.Text.Json.Nodes.JsonObject(),
            Status = InboundDocumentStatus.PendingPoints
        });
        await integration.SaveChangesAsync();

        var service = new PointsPostingService(ledger, customers, integration, products, new HardcodedRulesProvider());
        await service.ProcessPendingInvoicesAsync(10);

        var doc = await integration.InboundDocuments.FirstAsync();
        Assert.Equal(InboundDocumentStatus.Failed, doc.Status);
        Assert.NotNull(doc.Error);
    }

    [Fact]
    public async Task AwardInvoiceAsync_WritesAppliedRulesSnapshot()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        var customer = new Customer { TenantId = tenantId, Name = "Cust", ExternalId = "CUST-1" };
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        ledger.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        var rule = new MetadataInvoicePointsRule(
            new SpendRule(100m, 10),
            new InvoiceRuleMetadata(Guid.NewGuid(), "spend", 0, true, DateTimeOffset.UtcNow, null, JsonDocument.Parse("{}")));

        var service = new PointsPostingService(ledger, customers, integration, products, new SingleRuleProvider(rule));
        await service.AwardInvoiceAsync(new InvoiceUpsertRequest
        {
            TenantId = tenantId,
            InvoiceId = "INV-meta",
            CustomerExternalId = "CUST-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest> { new() { Sku = "A", Quantity = 1, NetAmount = 200m } }
        });

        var tx = await ledger.PointsTransactions.FirstAsync();
        Assert.NotNull(tx.AppliedRules);
    }

    [Fact]
    public async Task AwardInvoiceAsync_MapsCustomerBusinessSegmentAndRegion_ForComplexRuleConditions()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);

        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            TenantId = tenantId,
            Name = "Blue Fox Bar",
            ExternalId = "CUST-BLUE-001",
            BusinessSegment = "Modern On Trade (MONT)",
            Address = new CustomerAddress { Region = "GP" }
        };
        customers.Customers.Add(customer);
        await customers.SaveChangesAsync();
        ledger.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });
        await ledger.SaveChangesAsync();

        var ruleId = Guid.NewGuid();
        var rootGroup = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = ruleId,
            Logic = "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        static RuleCondition C(Guid groupId, string entity, string attribute, string op, string json, int sort) =>
            new()
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                EntityCode = entity,
                AttributeCode = attribute,
                Operator = op,
                ValueJson = JsonDocument.Parse(json),
                SortOrder = sort,
                CreatedAt = DateTimeOffset.UtcNow
            };

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints: 80,
            groups: new[] { rootGroup },
            conditions: new[]
            {
                C(rootGroup.Id, "product", "sku", "in", "[\"BEER-HEINEKEN-BTL-24PK\",\"BEER-HEINEKEN-CAN-6PK\"]", 0),
                C(rootGroup.Id, "product", "quantity", "gte", "10", 1),
                C(rootGroup.Id, "customer", "channel", "eq", "\"mont\"", 2),
                C(rootGroup.Id, "customer", "region", "eq", "\"gp\"", 3)
            });

        var service = new PointsPostingService(ledger, customers, integration, products, new SingleRuleProvider(rule));
        var response = await service.AwardInvoiceAsync(new InvoiceUpsertRequest
        {
            TenantId = tenantId,
            InvoiceId = "INV-COND-1",
            CustomerExternalId = "CUST-BLUE-001",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "BEER-HEINEKEN-BTL-24PK", Quantity = 10, NetAmount = 3899.90m },
                new() { Sku = "BEER-IPA-CAN-12PK", Quantity = 10, NetAmount = 3174.90m }
            }
        });

        Assert.Equal(80, response.PointsAwarded);
        var account = await ledger.PointsAccounts.FirstAsync(a => a.CustomerId == customer.Id);
        Assert.Equal(80, account.Balance);
    }

    [Fact]
    public async Task ProcessPendingInvoicesAsync_IgnoresInvalidBatchSize()
    {
        var dbName = Guid.NewGuid().ToString();
        var (ledger, customers, integration, products) = CreateContexts(dbName);
        var service = new PointsPostingService(ledger, customers, integration, products, new HardcodedRulesProvider());

        await service.ProcessPendingInvoicesAsync(0);
    }

    private sealed class SingleRuleProvider : IInvoicePointsRuleProvider
    {
        private readonly IInvoicePointsRule _rule;

        public SingleRuleProvider(IInvoicePointsRule rule) => _rule = rule;

        public Task<IReadOnlyList<IInvoicePointsRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IInvoicePointsRule>>(new[] { _rule });
    }
}
