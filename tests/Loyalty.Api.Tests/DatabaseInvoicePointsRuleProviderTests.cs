using System;
using System.Text.Json;
using System.Threading.Tasks;
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
            RuleVersion = 1,
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
            RuleVersion = 1,
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
            RuleVersion = 1,
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
}
