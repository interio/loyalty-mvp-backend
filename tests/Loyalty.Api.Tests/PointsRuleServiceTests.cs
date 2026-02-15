using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsRuleServiceTests
{
    private static IntegrationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new IntegrationDbContext(options);
    }

    [Fact]
    public async Task UpsertAsync_ValidatesRequests()
    {
        await using var db = CreateContext();
        var service = new PointsRuleService(db);

        var exEmpty = await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertAsync(new PointsRuleUpsertRequest[] { }));
        Assert.Contains("At least one rule", exEmpty.Message);

        var exTenant = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new PointsRuleUpsertRequest
                {
                    TenantId = Guid.Empty,
                    Name = "Rule 1",
                    RuleType = "spend"
                }
            }));
        Assert.Contains("tenantId is required", exTenant.Message);

        var exName = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new PointsRuleUpsertRequest
                {
                    TenantId = Guid.NewGuid(),
                    Name = " ",
                    RuleType = "spend"
                }
            }));
        Assert.Contains("name is required", exName.Message);

        var exType = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new PointsRuleUpsertRequest
                {
                    TenantId = Guid.NewGuid(),
                    Name = "Rule 2",
                    RuleType = " "
                }
            }));
        Assert.Contains("ruleType is required", exType.Message);
    }

    [Fact]
    public async Task UpsertAsync_CreateAndDelete()
    {
        await using var db = CreateContext();
        var service = new PointsRuleService(db);
        var tenantId = Guid.NewGuid();

        var request = new PointsRuleUpsertRequest
        {
            TenantId = tenantId,
            Name = "Spend rule A",
            RuleType = "spend",
            Active = true,
            Priority = 1,
            Conditions = new Dictionary<string, object?>
            {
                ["spendStep"] = 100,
                ["rewardPoints"] = 10
            }
        };

        await service.UpsertAsync(new[] { request });
        var rule = await db.PointsRules.FirstAsync();
        Assert.Equal(1, rule.RuleVersion);
        Assert.True(await db.RuleConditionGroups.AnyAsync(g => g.RuleId == rule.Id));
        Assert.True(await db.RuleConditions.AnyAsync());

        request.Id = rule.Id;
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertAsync(new[] { request }));

        Assert.True(await service.ExistsAsync(rule.Id, tenantId));

        var list = await service.ListByTenantAsync(tenantId);
        Assert.Single(list);

        await service.DeleteAsync(rule.Id, tenantId);
        Assert.False(await service.ExistsAsync(rule.Id, tenantId));
        Assert.False(await db.RuleConditionGroups.AnyAsync(g => g.RuleId == rule.Id));
        Assert.False(await db.RuleConditions.AnyAsync());
    }

    [Fact]
    public async Task SetActiveAsync_UpdatesRule()
    {
        await using var db = CreateContext();
        var service = new PointsRuleService(db);

        var tenantId = Guid.NewGuid();
        await service.UpsertAsync(new[]
        {
            new PointsRuleUpsertRequest
            {
                TenantId = tenantId,
                Name = "Spend rule A",
                RuleType = "spend",
                Active = true,
                Priority = 1
            }
        });

        var rule = await db.PointsRules.FirstAsync();
        await service.SetActiveAsync(rule.Id, tenantId, false);

        var updated = await db.PointsRules.FirstAsync();
        Assert.False(updated.Active);
        Assert.Equal(2, updated.RuleVersion);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenMissing()
    {
        await using var db = CreateContext();
        var service = new PointsRuleService(db);

        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(() =>
            service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
