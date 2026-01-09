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

        db.PointsRules.Add(new PointsRule
        {
            TenantId = tenantId,
            RuleType = "spend",
            Active = true,
            Priority = 0,
            RuleVersion = 1,
            Conditions = JsonDocument.Parse("{\"spendStep\":100,\"rewardPoints\":10}"),
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
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

        db.PointsRules.Add(new PointsRule
        {
            TenantId = tenantId,
            RuleType = "unknown",
            Active = true,
            Priority = 0,
            RuleVersion = 1,
            Conditions = JsonDocument.Parse("{\"foo\":1}"),
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
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

        db.PointsRules.Add(new PointsRule
        {
            TenantId = tenantId,
            RuleType = "spend",
            Active = true,
            Priority = 0,
            RuleVersion = 1,
            Conditions = JsonDocument.Parse("{\"spendStep\":0,\"rewardPoints\":0}"),
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        await db.SaveChangesAsync();

        var provider = new DatabaseInvoicePointsRuleProvider(db, new NullLogger<DatabaseInvoicePointsRuleProvider>());
        var rules = await provider.GetRulesAsync(tenantId);

        Assert.Empty(rules);
    }
}
