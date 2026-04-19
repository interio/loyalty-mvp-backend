using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Xunit;

namespace Loyalty.Api.Tests;

public class ComplexRuleEntityEvaluatorTests
{
    [Fact]
    public void InvoiceEvaluator_ResolvesKnownInvoiceFields()
    {
        var evaluator = new ComplexRuleInvoiceEntityEvaluator();
        var invoice = new InvoiceUpsertRequest
        {
            TenantId = Guid.NewGuid(),
            InvoiceId = "INV-1",
            OccurredAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CustomerExternalId = "CUST-1",
            Currency = "EUR",
            ActorEmail = "a@test",
            ActorExternalId = "USR-1",
            CustomerTier = "gold",
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "A", Quantity = 1, NetAmount = 20 },
                new() { Sku = "B", Quantity = 2, NetAmount = 30 }
            }
        };
        var context = new ComplexRuleEvaluationContext(invoice, null, new Dictionary<string, JsonObject>());

        Assert.True(evaluator.Evaluate(
            CreateCondition("invoice", "currency", "eq", "\"EUR\""),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("invoice", "totalAmount", "eq", "50"),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("invoice", "qty", "eq", "3"),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("invoice", "productQty", "eq", "3"),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("invoice", "linesCount", "eq", "2"),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("invoice", "tenantId", "eq", $"\"{invoice.TenantId}\""),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("invoice", "customerTier", "eq", "\"gold\""),
            context));
    }

    [Fact]
    public void ProductEvaluator_ResolvesCoreFields_AndAttributeFallback()
    {
        var evaluator = new ComplexRuleProductEntityEvaluator();
        var line = new InvoiceLineRequest { Sku = "SKU-1", Quantity = 3, NetAmount = 99.5m, DistributorId = "dist-1" };
        var invoice = new InvoiceUpsertRequest
        {
            TenantId = Guid.NewGuid(),
            InvoiceId = "INV-1",
            OccurredAt = DateTimeOffset.UtcNow,
            CustomerExternalId = "CUST-1",
            Lines = new List<InvoiceLineRequest> { line }
        };
        var attrs = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
        {
            ["SKU-1"] = new JsonObject
            {
                ["Category Name"] = "Beer"
            }
        };
        var context = new ComplexRuleEvaluationContext(invoice, line, attrs);

        Assert.True(evaluator.Evaluate(
            CreateCondition("product", "sku", "eq", "\"SKU-1\""),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("product", "quantity", "eq", "3"),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("product", "quantityInOrder", "eq", "3"),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("product", "netAmount", "eq", "99.5"),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("product", "distributorId", "eq", "\"dist-1\""),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("product", "category_name", "eq", "\"beer\""),
            context));
    }

    [Fact]
    public void CustomerEvaluator_ResolvesTierExternalIdChannelAndRegion()
    {
        var evaluator = new ComplexRuleCustomerEntityEvaluator();
        var invoice = new InvoiceUpsertRequest
        {
            TenantId = Guid.NewGuid(),
            InvoiceId = "INV-1",
            OccurredAt = DateTimeOffset.UtcNow,
            CustomerExternalId = "CUST-1",
            CustomerTier = "platinum",
            CustomerChannel = "Modern On Trade (MONT)",
            CustomerRegion = "GP",
            Lines = new List<InvoiceLineRequest> { new() { Sku = "SKU-1", Quantity = 1, NetAmount = 10 } }
        };
        var context = new ComplexRuleEvaluationContext(invoice, null, new Dictionary<string, JsonObject>());

        Assert.True(evaluator.Evaluate(
            CreateCondition("customer", "tier", "eq", "\"platinum\""),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("customer", "externalId", "eq", "\"CUST-1\""),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("customer", "channel", "eq", "\"mont\""),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("customer", "channel", "eq", "\"%on trade%\""),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("customer", "channel", "in", "[\"off\",\"mont\"]"),
            context));
        Assert.True(evaluator.Evaluate(
            CreateCondition("customer", "region", "eq", "\"gp\""),
            context));
        Assert.False(evaluator.Evaluate(
            CreateCondition("customer", "region", "eq", "\"wc\""),
            context));
    }

    [Fact]
    public void DistributorEvaluator_ResolvesDistributorId()
    {
        var evaluator = new ComplexRuleDistributorEntityEvaluator();
        var line = new InvoiceLineRequest { Sku = "SKU-1", Quantity = 1, NetAmount = 10, DistributorId = "111-111-xxx" };
        var invoice = new InvoiceUpsertRequest
        {
            TenantId = Guid.NewGuid(),
            InvoiceId = "INV-1",
            OccurredAt = DateTimeOffset.UtcNow,
            CustomerExternalId = "CUST-1",
            Lines = new List<InvoiceLineRequest> { line }
        };
        var context = new ComplexRuleEvaluationContext(invoice, line, new Dictionary<string, JsonObject>());

        Assert.True(evaluator.Evaluate(
            CreateCondition("distributor", "id", "eq", "\"111-111-xxx\""),
            context));
    }

    [Fact]
    public void EntityEvaluatorRegistry_ContainsDefaultEvaluators()
    {
        var registry = new ComplexRuleEntityEvaluatorRegistry(null);

        Assert.True(registry.TryGet("invoice", out var invoice));
        Assert.True(registry.TryGet("customer", out var customer));
        Assert.True(registry.TryGet("distributor", out var distributor));
        Assert.True(registry.TryGet("product", out var product));
        Assert.Equal(ComplexRuleEntityScope.Invoice, invoice.Scope);
        Assert.Equal(ComplexRuleEntityScope.Invoice, customer.Scope);
        Assert.Equal(ComplexRuleEntityScope.InvoiceLine, distributor.Scope);
        Assert.Equal(ComplexRuleEntityScope.InvoiceLine, product.Scope);
        Assert.True(registry.IsLineScoped("distributor"));
        Assert.True(registry.IsLineScoped("product"));
    }

    [Fact]
    public void EntityEvaluatorRegistry_AllowsOverrides()
    {
        var registry = new ComplexRuleEntityEvaluatorRegistry(new[] { new InvoiceOverrideEvaluator() });

        Assert.True(registry.TryGet("invoice", out var evaluator));
        Assert.IsType<InvoiceOverrideEvaluator>(evaluator);
    }

    private static RuleCondition CreateCondition(string entityCode, string attributeCode, string op, string jsonValue)
    {
        return new RuleCondition
        {
            Id = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            EntityCode = entityCode,
            AttributeCode = attributeCode,
            Operator = op,
            ValueJson = JsonDocument.Parse(jsonValue),
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class InvoiceOverrideEvaluator : IComplexRuleEntityEvaluator
    {
        public string EntityCode => "invoice";

        public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.Invoice;

        public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context) => true;
    }
}
