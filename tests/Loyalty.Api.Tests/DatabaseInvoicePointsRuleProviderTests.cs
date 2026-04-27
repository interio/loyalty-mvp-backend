using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Loyalty.Api.Tests;

public class DatabaseInvoicePointsRuleProviderTests
{
    private static IntegrationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IntegrationDbContext(options);
    }

    [Fact]
    public async Task GetRulesAsync_ParsesSupportedRules()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var rule = new PointsRule
        {
            TenantId = tenantId,
            Name = "Spend",
            RuleType = "spend",
            Active = true,
            Priority = 0,
            RewardPoints = 10,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var rootGroup = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            Logic = "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        rule.RootGroupId = rootGroup.Id;

        db.PointsRules.Add(rule);
        db.RuleConditionGroups.Add(rootGroup);
        db.RuleConditions.Add(new RuleCondition
        {
            GroupId = rootGroup.Id,
            EntityCode = "rule",
            AttributeCode = "spendStep",
            Operator = "eq",
            ValueJson = JsonDocument.Parse("100"),
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var provider = new DatabaseInvoicePointsRuleProvider(db, new NullLogger<DatabaseInvoicePointsRuleProvider>());
        var rules = await provider.GetRulesAsync(tenantId);

        Assert.Single(rules);
        Assert.Equal("Spend(100->10)", rules[0].Name);
    }

    [Fact]
    public async Task GetRulesAsync_SkipsInvalidRules()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var rule = new PointsRule
        {
            TenantId = tenantId,
            Name = "Unknown",
            RuleType = "unknown",
            Active = true,
            Priority = 0,
            RewardPoints = 10,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var rootGroup = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            Logic = "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        rule.RootGroupId = rootGroup.Id;

        db.PointsRules.Add(rule);
        db.RuleConditionGroups.Add(rootGroup);
        db.RuleConditions.Add(new RuleCondition
        {
            GroupId = rootGroup.Id,
            EntityCode = "rule",
            AttributeCode = "foo",
            Operator = "eq",
            ValueJson = JsonDocument.Parse("1"),
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var provider = new DatabaseInvoicePointsRuleProvider(db, new NullLogger<DatabaseInvoicePointsRuleProvider>());
        var rules = await provider.GetRulesAsync(tenantId);

        Assert.Empty(rules);
    }

    [Fact]
    public async Task GetRulesAsync_IgnoresWelcomeBonusRules_ForInvoiceEvaluation()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var rule = new PointsRule
        {
            TenantId = tenantId,
            Name = "Welcome bonus",
            RuleType = "welcome_bonus",
            Active = true,
            Priority = 0,
            RewardPoints = 100,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        db.PointsRules.Add(rule);
        await db.SaveChangesAsync();

        var provider = new DatabaseInvoicePointsRuleProvider(db, new NullLogger<DatabaseInvoicePointsRuleProvider>());
        var rules = await provider.GetRulesAsync(tenantId);

        Assert.Empty(rules);
    }

    [Fact]
    public async Task GetRulesAsync_RequiresValidConditions()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var rule = new PointsRule
        {
            TenantId = tenantId,
            Name = "Spend",
            RuleType = "spend",
            Active = true,
            Priority = 0,
            RewardPoints = 0,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var rootGroup = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            Logic = "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        rule.RootGroupId = rootGroup.Id;

        db.PointsRules.Add(rule);
        db.RuleConditionGroups.Add(rootGroup);
        db.RuleConditions.AddRange(
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "spendStep",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("0"),
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "rewardPoints",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("0"),
                SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });

        await db.SaveChangesAsync();

        var provider = new DatabaseInvoicePointsRuleProvider(db, new NullLogger<DatabaseInvoicePointsRuleProvider>());
        var rules = await provider.GetRulesAsync(tenantId);

        Assert.Empty(rules);
    }

    [Fact]
    public async Task GetRulesAsync_FallsBackToLegacyRewardPointsCondition()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var rule = new PointsRule
        {
            TenantId = tenantId,
            Name = "Spend Legacy",
            RuleType = "spend",
            Active = true,
            Priority = 0,
            RewardPoints = 0,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var rootGroup = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            Logic = "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        rule.RootGroupId = rootGroup.Id;

        db.PointsRules.Add(rule);
        db.RuleConditionGroups.Add(rootGroup);
        db.RuleConditions.AddRange(
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "spendStep",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("100"),
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "rewardPoints",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("10"),
                SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });

        await db.SaveChangesAsync();

        var provider = new DatabaseInvoicePointsRuleProvider(db, new NullLogger<DatabaseInvoicePointsRuleProvider>());
        var rules = await provider.GetRulesAsync(tenantId);

        Assert.Single(rules);
        Assert.Equal("Spend(100->10)", rules[0].Name);
    }

    [Fact]
    public async Task GetRulesAsync_SkuQuantityCampaign_AwardsPointsPerSku()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var rule = new PointsRule
        {
            TenantId = tenantId,
            Name = "SKU quantity",
            RuleType = "sku_quantity",
            Active = true,
            Priority = 0,
            RewardPoints = 10,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var rootGroup = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            Logic = "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        rule.RootGroupId = rootGroup.Id;

        db.PointsRules.Add(rule);
        db.RuleConditionGroups.Add(rootGroup);
        db.RuleConditions.AddRange(
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "skus",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("[\"SKU1\",\"SKU2\",\"SKU3\"]"),
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "quantityStep",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("10"),
                SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });

        await db.SaveChangesAsync();

        var provider = new DatabaseInvoicePointsRuleProvider(db, new NullLogger<DatabaseInvoicePointsRuleProvider>());
        var rules = await provider.GetRulesAsync(tenantId);

        Assert.Single(rules);

        var points = rules[0].CalculatePoints(new InvoiceUpsertRequest
        {
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "SKU1", Quantity = 10m, NetAmount = 0 },
                new() { Sku = "SKU2", Quantity = 9m, NetAmount = 0 },
                new() { Sku = "SKU3", Quantity = 20m, NetAmount = 0 }
            }
        });

        Assert.Equal(30, points);
    }

    [Fact]
    public async Task GetRulesAsync_ComplexPerCurrency_AwardsPointsFromMatchingLineAmounts()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();

        var rule = new PointsRule
        {
            TenantId = tenantId,
            Name = "Complex per currency",
            RuleType = "complex_rule",
            Active = true,
            Priority = 0,
            RewardPoints = 0,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var rootGroup = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            Logic = "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        rule.RootGroupId = rootGroup.Id;

        db.PointsRules.Add(rule);
        db.RuleConditionGroups.Add(rootGroup);
        db.RuleConditions.AddRange(
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "product",
                AttributeCode = "sku",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("\"SKU-1\""),
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "awardMode",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("\"per_currency\""),
                SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "pointsPerCurrencyPoints",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("2"),
                SortOrder = 2,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RuleCondition
            {
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "pointsPerCurrencyAmount",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("25"),
                SortOrder = 3,
                CreatedAt = DateTimeOffset.UtcNow
            });

        await db.SaveChangesAsync();

        var provider = new DatabaseInvoicePointsRuleProvider(db, new NullLogger<DatabaseInvoicePointsRuleProvider>());
        var rules = await provider.GetRulesAsync(tenantId);

        Assert.Single(rules);

        var points = rules[0].CalculatePoints(new InvoiceUpsertRequest
        {
            TenantId = tenantId,
            InvoiceId = "INV-1",
            CustomerExternalId = "CUST-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Lines = new List<InvoiceLineRequest>
            {
                new() { Sku = "SKU-1", Quantity = 1, NetAmount = 140m },
                new() { Sku = "SKU-2", Quantity = 1, NetAmount = 500m }
            }
        });

        Assert.Equal(10, points);
    }
}
