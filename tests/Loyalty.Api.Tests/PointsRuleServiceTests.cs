using System;
using System.Linq;
using System.Threading.Tasks;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class PointsRuleServiceTests
{
    private static IntegrationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
                    RuleType = "spend"
                }
            }));
        Assert.Contains("tenantId is required", exTenant.Message);

        var exType = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpsertAsync(new[]
            {
                new PointsRuleUpsertRequest
                {
                    TenantId = Guid.NewGuid(),
                    RuleType = " "
                }
            }));
        Assert.Contains("ruleType is required", exType.Message);
    }

    [Fact]
    public async Task UpsertAsync_CreateUpdateAndDelete()
    {
        await using var db = CreateContext();
        var service = new PointsRuleService(db);
        var tenantId = Guid.NewGuid();

        var request = new PointsRuleUpsertRequest
        {
            TenantId = tenantId,
            RuleType = "spend",
            Active = true,
            Priority = 1
        };

        await service.UpsertAsync(new[] { request });
        var rule = await db.PointsRules.FirstAsync();
        Assert.Equal(1, rule.RuleVersion);

        request.Id = rule.Id;
        request.Priority = 2;
        await service.UpsertAsync(new[] { request });

        var updated = await db.PointsRules.FirstAsync();
        Assert.Equal(2, updated.Priority);
        Assert.Equal(2, updated.RuleVersion);

        Assert.True(await service.ExistsAsync(rule.Id));

        var list = await service.ListByTenantAsync(tenantId);
        Assert.Single(list);

        await service.DeleteAsync(rule.Id);
        Assert.False(await service.ExistsAsync(rule.Id));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenMissing()
    {
        await using var db = CreateContext();
        var service = new PointsRuleService(db);

        await Assert.ThrowsAsync<System.Collections.Generic.KeyNotFoundException>(() =>
            service.DeleteAsync(Guid.NewGuid()));
    }
}
