using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Loyalty.Api.Tests;

public class CustomerWelcomeBonusServiceTests
{
    private static (CustomersDbContext customers, LedgerDbContext ledger, IntegrationDbContext integration) CreateContexts()
    {
        var dbName = Guid.NewGuid().ToString();
        var customersOptions = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ledgerOptions = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var integrationOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return (
            new CustomersDbContext(customersOptions),
            new LedgerDbContext(ledgerOptions),
            new IntegrationDbContext(integrationOptions));
    }

    [Fact]
    public async Task AwardAsync_AwardsOnlyOnce_ForEligibleCustomer()
    {
        var (customersDb, ledgerDb, integrationDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        await using var idb = integrationDb;

        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Blue Fox",
            ExternalId = "CUST-BLUE-001",
            Tier = CustomerTierCatalog.Silver,
            Status = CustomerStatusCatalog.Active,
            BusinessSegment = "Modern On Trade (MONT)",
            Address = new CustomerAddress { Region = "GP" },
            OnboardDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        cd.Customers.Add(customer);
        ld.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });

        var rule = new PointsRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Welcome bonus",
            RuleType = "welcome_bonus",
            RewardPoints = 120,
            Active = true,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            Priority = 0
        };
        idb.PointsRules.Add(rule);

        await cd.SaveChangesAsync();
        await ld.SaveChangesAsync();
        await idb.SaveChangesAsync();

        var service = new CustomerWelcomeBonusService(cd, ld, idb, Array.Empty<IComplexRuleEntityEvaluator>());

        var first = await service.AwardAsync(new AwardWelcomeBonusCommand(customer.Id, tenantId, "admin@test", true));
        Assert.True(first.Awarded);
        Assert.Equal(120, first.PointsAwarded);
        Assert.Equal("awarded", first.Outcome);

        var second = await service.AwardAsync(new AwardWelcomeBonusCommand(customer.Id, tenantId, "admin@test", true));
        Assert.False(second.Awarded);
        Assert.Equal("already_awarded", second.Outcome);

        var account = await ld.PointsAccounts.FirstAsync(a => a.CustomerId == customer.Id);
        Assert.Equal(120, account.Balance);
        Assert.Equal(1, await ld.PointsTransactions.CountAsync(t => t.CustomerId == customer.Id && t.Reason == PointsReasons.WelcomeBonus));
    }

    [Fact]
    public async Task AwardAsync_WithCustomerConditions_AppliesOnlyWhenMatched()
    {
        var (customersDb, ledgerDb, integrationDb) = CreateContexts();
        await using var cd = customersDb;
        await using var ld = ledgerDb;
        await using var idb = integrationDb;

        var tenantId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Green Bar",
            ExternalId = "CUST-GREEN-001",
            Tier = CustomerTierCatalog.Gold,
            Status = CustomerStatusCatalog.Active,
            BusinessSegment = "Modern On Trade (MONT)",
            Address = new CustomerAddress { Region = "GP" },
            OnboardDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        cd.Customers.Add(customer);
        ld.PointsAccounts.Add(new PointsAccount { CustomerId = customer.Id, Balance = 0 });

        var rule = new PointsRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Welcome bonus conditioned",
            RuleType = "welcome_bonus",
            RewardPoints = 80,
            Active = true,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            Priority = 0
        };
        var root = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            Logic = "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };
        rule.RootGroupId = root.Id;
        idb.PointsRules.Add(rule);
        idb.RuleConditionGroups.Add(root);
        idb.RuleConditions.AddRange(
            new RuleCondition
            {
                GroupId = root.Id,
                EntityCode = "customer",
                AttributeCode = "status",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("1"),
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new RuleCondition
            {
                GroupId = root.Id,
                EntityCode = "customer",
                AttributeCode = "region",
                Operator = "eq",
                ValueJson = JsonDocument.Parse("\"GP\""),
                SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });

        await cd.SaveChangesAsync();
        await ld.SaveChangesAsync();
        await idb.SaveChangesAsync();

        var service = new CustomerWelcomeBonusService(cd, ld, idb, Array.Empty<IComplexRuleEntityEvaluator>());
        var result = await service.AwardAsync(new AwardWelcomeBonusCommand(customer.Id, tenantId, null, true));

        Assert.True(result.Awarded);
        Assert.Equal(80, result.PointsAwarded);
    }
}
