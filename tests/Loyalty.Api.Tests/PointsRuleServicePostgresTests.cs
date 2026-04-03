using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsRuleServicePostgresTests
{
    [Fact]
    public async Task DeleteAsync_CascadesConditionTree()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var integration = TestDbContextFactory.CreateIntegration(db.ConnectionString);
        await integration.Database.EnsureCreatedAsync();

        var service = new PointsRuleService(integration);
        var tenantId = Guid.NewGuid();

        await service.UpsertAsync(new[]
        {
            new PointsRuleUpsertRequest
            {
                TenantId = tenantId,
                Name = "Cascade Delete Rule Via Service",
                RuleType = "spend",
                Active = true,
                Priority = 1,
                Conditions = new Dictionary<string, object?>
                {
                    ["spendStep"] = 100,
                    ["rewardPoints"] = 10
                }
            }
        });

        var rule = await integration.PointsRules.AsNoTracking().SingleAsync();
        Assert.Equal(10, rule.RewardPoints);
        var groupIds = await integration.RuleConditionGroups
            .AsNoTracking()
            .Where(g => g.RuleId == rule.Id)
            .Select(g => g.Id)
            .ToListAsync();

        Assert.NotEmpty(groupIds);
        Assert.True(await integration.RuleConditions.AnyAsync(c => groupIds.Contains(c.GroupId)));
        Assert.False(await integration.RuleConditions.AnyAsync(c => c.GroupId == rule.RootGroupId && c.AttributeCode == "rewardPoints"));

        await service.DeleteAsync(rule.Id, tenantId);

        Assert.False(await integration.PointsRules.AnyAsync(r => r.Id == rule.Id));
        Assert.False(await integration.RuleConditionGroups.AnyAsync(g => g.RuleId == rule.Id));
        Assert.False(await integration.RuleConditions.AnyAsync(c => groupIds.Contains(c.GroupId)));
    }

    [Fact]
    public async Task DeleteViaSql_CascadesConditionTree()
    {
        await using var db = await PostgresTestDatabase.CreateAsync();
        await using var integration = TestDbContextFactory.CreateIntegration(db.ConnectionString);
        await integration.Database.EnsureCreatedAsync();

        var service = new PointsRuleService(integration);
        var tenantId = Guid.NewGuid();

        await service.UpsertAsync(new[]
        {
            new PointsRuleUpsertRequest
            {
                TenantId = tenantId,
                Name = "Cascade Delete Rule",
                RuleType = "spend",
                Active = true,
                Priority = 1,
                Conditions = new Dictionary<string, object?>
                {
                    ["spendStep"] = 100,
                    ["rewardPoints"] = 10
                }
            }
        });

        var rule = await integration.PointsRules.AsNoTracking().SingleAsync();
        Assert.Equal(10, rule.RewardPoints);
        var groupIds = await integration.RuleConditionGroups
            .AsNoTracking()
            .Where(g => g.RuleId == rule.Id)
            .Select(g => g.Id)
            .ToListAsync();

        Assert.NotEmpty(groupIds);
        Assert.True(await integration.RuleConditions.AnyAsync(c => groupIds.Contains(c.GroupId)));
        Assert.False(await integration.RuleConditions.AnyAsync(c => c.GroupId == rule.RootGroupId && c.AttributeCode == "rewardPoints"));

        await integration.Database.ExecuteSqlInterpolatedAsync(
            $@"DELETE FROM ""PointsRules"" WHERE ""Id"" = {rule.Id}");

        Assert.False(await integration.PointsRules.AnyAsync(r => r.Id == rule.Id));
        Assert.False(await integration.RuleConditionGroups.AnyAsync(g => g.RuleId == rule.Id));
        Assert.False(await integration.RuleConditions.AnyAsync(c => groupIds.Contains(c.GroupId)));
    }
}
