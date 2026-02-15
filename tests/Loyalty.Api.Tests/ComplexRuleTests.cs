using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Xunit;

namespace Loyalty.Api.Tests;

public class ComplexRuleTests
{
    [Fact]
    public void CalculatePoints_MixedInvoiceAndProductAnd_MatchesSingleLine()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var rewardPoints = 25;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup },
            new[]
            {
                CreateCondition(rootGroup.Id, "invoice", "currency", "eq", "\"EUR\"", 0),
                CreateCondition(rootGroup.Id, "product", "sku", "eq", "\"SKU-1\"", 1),
                CreateCondition(rootGroup.Id, "product", "quantity", "gte", "2", 2)
            });

        var invoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[] { new InvoiceLineRequest { Sku = "SKU-1", Quantity = 2, NetAmount = 100 } });

        var points = rule.CalculatePoints(invoice);

        Assert.Equal(rewardPoints, points);
    }

    [Fact]
    public void CalculatePoints_ProductConditionsMustMatchSameLine_ForAndGroups()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var rewardPoints = 30;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup },
            new[]
            {
                CreateCondition(rootGroup.Id, "product", "sku", "eq", "\"SKU-1\"", 0),
                CreateCondition(rootGroup.Id, "product", "quantity", "gte", "2", 1)
            });

        var invoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[]
            {
                new InvoiceLineRequest { Sku = "SKU-1", Quantity = 1, NetAmount = 50 },
                new InvoiceLineRequest { Sku = "OTHER", Quantity = 3, NetAmount = 70 }
            });

        var points = rule.CalculatePoints(invoice);

        Assert.Equal(0, points);
    }

    [Fact]
    public void CalculatePoints_NestedOrChildGroup_IsEvaluated()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var productOrGroup = CreateGroup(ruleId, rootGroup.Id, "OR", 1);
        var rewardPoints = 40;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup, productOrGroup },
            new[]
            {
                CreateCondition(rootGroup.Id, "invoice", "currency", "eq", "\"EUR\"", 0),
                CreateCondition(productOrGroup.Id, "product", "sku", "eq", "\"SKU-A\"", 0),
                CreateCondition(productOrGroup.Id, "product", "sku", "eq", "\"SKU-B\"", 1)
            });

        var invoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[] { new InvoiceLineRequest { Sku = "SKU-B", Quantity = 1, NetAmount = 20 } });

        var points = rule.CalculatePoints(invoice);

        Assert.Equal(rewardPoints, points);
    }

    [Fact]
    public void CalculatePoints_UsesProductAttributes_WhenCoreProductFieldNotFound()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var rewardPoints = 50;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup },
            new[] { CreateCondition(rootGroup.Id, "product", "category", "eq", "\"beer\"", 0) });

        rule.SetProductAttributes(new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
        {
            ["SKU-X"] = new JsonObject { ["Category"] = "Beer" }
        });

        var invoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[] { new InvoiceLineRequest { Sku = "SKU-X", Quantity = 1, NetAmount = 15 } });

        var points = rule.CalculatePoints(invoice);

        Assert.Equal(rewardPoints, points);
    }

    [Fact]
    public void CalculatePoints_IgnoresRuleMetadataConditions()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var rewardPoints = 10;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup },
            new[]
            {
                CreateCondition(rootGroup.Id, "invoice", "currency", "eq", "\"EUR\"", 0),
                CreateCondition(rootGroup.Id, "rule", "rewardPoints", "eq", "999", 1)
            });

        var invoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[] { new InvoiceLineRequest { Sku = "SKU-X", Quantity = 1, NetAmount = 15 } });

        var points = rule.CalculatePoints(invoice);

        Assert.Equal(rewardPoints, points);
    }

    [Fact]
    public void CalculatePoints_SupportsDateAndSetOperators()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var rewardPoints = 15;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup },
            new[]
            {
                CreateCondition(rootGroup.Id, "invoice", "occurredAt", "gte", "\"2024-01-01T00:00:00Z\"", 0),
                CreateCondition(rootGroup.Id, "invoice", "currency", "in", "[\"USD\",\"EUR\"]", 1)
            });

        var invoice = CreateInvoice(
            currency: "eur",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[] { new InvoiceLineRequest { Sku = "SKU-X", Quantity = 1, NetAmount = 15 } });

        var points = rule.CalculatePoints(invoice);

        Assert.Equal(rewardPoints, points);
    }

    [Fact]
    public void CalculatePoints_SupportsCustomerTierAndDistributorIdConditions()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var rewardPoints = 500;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup },
            new[]
            {
                CreateCondition(rootGroup.Id, "customer", "tier", "in", "[\"gold\",\"platinum\"]", 0),
                CreateCondition(rootGroup.Id, "distributor", "id", "eq", "\"111-111-xxx\"", 1)
            });

        var matchingInvoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[]
            {
                new InvoiceLineRequest { Sku = "SKU-1", Quantity = 1, NetAmount = 30, DistributorId = "DIST-OTHER" },
                new InvoiceLineRequest { Sku = "SKU-2", Quantity = 2, NetAmount = 60, DistributorId = "111-111-xxx" }
            });
        matchingInvoice.CustomerTier = "gold";

        var tierMismatchInvoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[]
            {
                new InvoiceLineRequest { Sku = "SKU-2", Quantity = 2, NetAmount = 60, DistributorId = "111-111-xxx" }
            });
        tierMismatchInvoice.CustomerTier = "silver";

        var distributorMismatchInvoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[]
            {
                new InvoiceLineRequest { Sku = "SKU-2", Quantity = 2, NetAmount = 60, DistributorId = "DIST-OTHER" }
            });
        distributorMismatchInvoice.CustomerTier = "platinum";

        Assert.Equal(rewardPoints, rule.CalculatePoints(matchingInvoice));
        Assert.Equal(0, rule.CalculatePoints(tierMismatchInvoice));
        Assert.Equal(0, rule.CalculatePoints(distributorMismatchInvoice));
    }

    [Fact]
    public void CalculatePoints_UsesCustomInvoiceEntityEvaluator()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var rewardPoints = 12;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup },
            new[]
            {
                CreateCondition(rootGroup.Id, "invoice", "currency", "eq", "\"EUR\"", 0),
                CreateCondition(rootGroup.Id, "customer", "segment", "eq", "\"gold\"", 1)
            },
            new[] { new CustomerEntityEvaluator() });

        var invoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[] { new InvoiceLineRequest { Sku = "SKU-1", Quantity = 1, NetAmount = 20 } });

        var points = rule.CalculatePoints(invoice);

        Assert.Equal(rewardPoints, points);
    }

    [Fact]
    public void CalculatePoints_UsesCustomLineScopedEvaluator_WithSameLineSemantics()
    {
        var ruleId = Guid.NewGuid();
        var rootGroup = CreateGroup(ruleId, null, "AND", 0);
        var rewardPoints = 18;

        var rule = new ComplexRule(
            ruleId,
            rootGroup.Id,
            rewardPoints,
            new[] { rootGroup },
            new[]
            {
                CreateCondition(rootGroup.Id, "distributor", "code", "eq", "\"DIST-A\"", 0),
                CreateCondition(rootGroup.Id, "product", "quantity", "gte", "2", 1)
            },
            new[] { new DistributorEntityEvaluator() });

        var mismatchInvoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[]
            {
                new InvoiceLineRequest { Sku = "A-1", Quantity = 1, NetAmount = 10 },
                new InvoiceLineRequest { Sku = "B-1", Quantity = 3, NetAmount = 30 }
            });

        var matchingInvoice = CreateInvoice(
            currency: "EUR",
            occurredAt: DateTimeOffset.UtcNow,
            lines: new[]
            {
                new InvoiceLineRequest { Sku = "A-1", Quantity = 3, NetAmount = 30 }
            });

        Assert.Equal(0, rule.CalculatePoints(mismatchInvoice));
        Assert.Equal(rewardPoints, rule.CalculatePoints(matchingInvoice));
    }

    private static InvoiceUpsertRequest CreateInvoice(
        string currency,
        DateTimeOffset occurredAt,
        IEnumerable<InvoiceLineRequest> lines)
    {
        return new InvoiceUpsertRequest
        {
            TenantId = Guid.NewGuid(),
            InvoiceId = $"INV-{Guid.NewGuid():N}",
            OccurredAt = occurredAt,
            CustomerExternalId = "CUST-1",
            Currency = currency,
            Lines = lines.ToList()
        };
    }

    private static RuleConditionGroup CreateGroup(Guid ruleId, Guid? parentGroupId, string logic, int sortOrder)
    {
        return new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = ruleId,
            ParentGroupId = parentGroupId,
            Logic = logic,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static RuleCondition CreateCondition(
        Guid groupId,
        string entityCode,
        string attributeCode,
        string op,
        string jsonValue,
        int sortOrder)
    {
        return new RuleCondition
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            EntityCode = entityCode,
            AttributeCode = attributeCode,
            Operator = op,
            ValueJson = JsonDocument.Parse(jsonValue),
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class CustomerEntityEvaluator : IComplexRuleEntityEvaluator
    {
        public string EntityCode => "customer";

        public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.Invoice;

        public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context)
        {
            var left = "gold";
            return ComplexRuleComparisonEngine.Compare(left, condition.Operator, condition.ValueJson.RootElement);
        }
    }

    private sealed class DistributorEntityEvaluator : IComplexRuleEntityEvaluator
    {
        public string EntityCode => "distributor";

        public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.InvoiceLine;

        public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context)
        {
            if (context.Line is null) return false;
            var left = context.Line.Sku.StartsWith("A-", StringComparison.OrdinalIgnoreCase)
                ? "DIST-A"
                : "DIST-B";
            return ComplexRuleComparisonEngine.Compare(left, condition.Operator, condition.ValueJson.RootElement);
        }
    }
}
