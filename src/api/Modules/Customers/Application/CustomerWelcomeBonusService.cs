using System.Text.Json;
using System.Text.Json.Nodes;
using System.Transactions;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Loyalty.Api.Modules.Customers.Application;

public record AwardWelcomeBonusCommand(
    Guid CustomerId,
    Guid TenantId,
    string? ActorEmail,
    bool RequireOnboardDateReached);

public record WelcomeBonusAwardResult(
    Guid CustomerId,
    bool Awarded,
    int PointsAwarded,
    long CurrentBalance,
    string Outcome,
    DateTimeOffset? AwardedAt);

public interface ICustomerWelcomeBonusService
{
    Task<WelcomeBonusAwardResult> AwardAsync(AwardWelcomeBonusCommand command, CancellationToken ct = default);
}

public class CustomerWelcomeBonusService : ICustomerWelcomeBonusService
{
    private readonly CustomersDbContext _customersDb;
    private readonly LedgerDbContext _ledgerDb;
    private readonly IntegrationDbContext _integrationDb;
    private readonly IReadOnlyList<IComplexRuleEntityEvaluator> _entityEvaluators;
    private bool ShouldShareTransaction => _customersDb.Database.IsRelational() && _ledgerDb.Database.IsRelational();

    public CustomerWelcomeBonusService(
        CustomersDbContext customersDb,
        LedgerDbContext ledgerDb,
        IntegrationDbContext integrationDb,
        IEnumerable<IComplexRuleEntityEvaluator> entityEvaluators)
    {
        _customersDb = customersDb;
        _ledgerDb = ledgerDb;
        _integrationDb = integrationDb;
        _entityEvaluators = ComplexRuleEntityEvaluatorDefaults
            .MergeWithDefaults(entityEvaluators)
            .ToList();
    }

    public async Task<WelcomeBonusAwardResult> AwardAsync(AwardWelcomeBonusCommand command, CancellationToken ct = default)
    {
        if (command.CustomerId == Guid.Empty)
            throw new ArgumentException("customerId is required.");
        if (command.TenantId == Guid.Empty)
            throw new ArgumentException("tenantId is required.");

        var now = DateTimeOffset.UtcNow;
        var customer = await _customersDb.Customers
            .FirstOrDefaultAsync(c => c.Id == command.CustomerId && c.TenantId == command.TenantId, ct);

        if (customer is null)
            throw new System.Collections.Generic.KeyNotFoundException("Customer not found for tenant.");

        var account = await _ledgerDb.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == customer.Id, ct);
        if (account is null)
            throw new Exception("Customer has no points account.");

        if (customer.WelcomeBonusAwarded)
        {
            return new WelcomeBonusAwardResult(
                customer.Id,
                false,
                0,
                account.Balance,
                "already_awarded",
                customer.WelcomeBonusAwardedAt);
        }

        if (command.RequireOnboardDateReached && customer.OnboardDate > now)
        {
            return new WelcomeBonusAwardResult(
                customer.Id,
                false,
                0,
                account.Balance,
                "onboard_date_not_reached",
                customer.WelcomeBonusAwardedAt);
        }

        var rules = await _integrationDb.PointsRules
            .AsNoTracking()
            .Where(r =>
                r.TenantId == customer.TenantId &&
                r.Active &&
                r.RuleType.ToLower() == PointsRuleTypes.WelcomeBonus &&
                r.EffectiveFrom <= now &&
                (r.EffectiveTo == null || r.EffectiveTo >= now))
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        if (rules.Count == 0)
        {
            return new WelcomeBonusAwardResult(
                customer.Id,
                false,
                0,
                account.Balance,
                "no_active_campaign",
                customer.WelcomeBonusAwardedAt);
        }

        var ruleIds = rules.Select(r => r.Id).ToList();
        var groups = await _integrationDb.RuleConditionGroups
            .AsNoTracking()
            .Where(g => ruleIds.Contains(g.RuleId))
            .ToListAsync(ct);
        var groupIds = groups.Select(g => g.Id).ToList();
        var conditions = groupIds.Count == 0
            ? new List<RuleCondition>()
            : await _integrationDb.RuleConditions
                .AsNoTracking()
                .Where(c => groupIds.Contains(c.GroupId))
                .ToListAsync(ct);

        var groupsByRule = groups
            .GroupBy(g => g.RuleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RuleConditionGroup>)g.ToList());
        var conditionsByGroup = conditions
            .GroupBy(c => c.GroupId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RuleCondition>)g.ToList());

        var invoiceContext = BuildCustomerContext(customer);
        var totalPoints = 0;
        var appliedRules = new List<WelcomeBonusAppliedRule>();

        foreach (var rule in rules)
        {
            if (rule.RewardPoints <= 0)
                continue;

            groupsByRule.TryGetValue(rule.Id, out var ruleGroups);
            ruleGroups ??= Array.Empty<RuleConditionGroup>();
            var ruleConditions = ruleGroups
                .SelectMany(g => conditionsByGroup.TryGetValue(g.Id, out var list) ? list : Array.Empty<RuleCondition>())
                .ToList();

            var hasCustomerConditions = ruleConditions.Any(c => !string.Equals(c.EntityCode, "rule", StringComparison.OrdinalIgnoreCase));
            if (!hasCustomerConditions)
            {
                totalPoints += rule.RewardPoints;
                appliedRules.Add(new WelcomeBonusAppliedRule(
                    rule.Id,
                    rule.Name,
                    rule.RuleType,
                    rule.Priority,
                    rule.EffectiveFrom,
                    rule.EffectiveTo,
                    BuildConditionsDocument(ruleConditions),
                    rule.RewardPoints));
                continue;
            }

            if (!rule.RootGroupId.HasValue)
                continue;
            if (!ruleGroups.Any(g => g.Id == rule.RootGroupId.Value))
                continue;
            if (ruleConditions.Any(c =>
                    !string.Equals(c.EntityCode, "rule", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.EntityCode, "customer", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var compiledRule = new ComplexRule(
                rule.Id,
                rule.RootGroupId.Value,
                rule.RewardPoints,
                ruleGroups,
                ruleConditions,
                _entityEvaluators);

            var awarded = compiledRule.CalculatePoints(invoiceContext);
            if (awarded <= 0)
                continue;

            totalPoints += awarded;
            appliedRules.Add(new WelcomeBonusAppliedRule(
                rule.Id,
                rule.Name,
                rule.RuleType,
                rule.Priority,
                rule.EffectiveFrom,
                rule.EffectiveTo,
                BuildConditionsDocument(ruleConditions),
                awarded));
        }

        if (totalPoints <= 0)
        {
            return new WelcomeBonusAwardResult(
                customer.Id,
                false,
                0,
                account.Balance,
                "no_matching_campaign",
                customer.WelcomeBonusAwardedAt);
        }

        var actorEmail = TrimOrNull(command.ActorEmail)?.ToLowerInvariant();
        var correlationId = $"welcome_bonus:{customer.Id}";
        var awardedAt = DateTimeOffset.UtcNow;

        if (ShouldShareTransaction)
        {
            try
            {
                var strategy = _customersDb.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

                    var customerTracked = await _customersDb.Customers
                        .FirstOrDefaultAsync(c => c.Id == customer.Id && c.TenantId == customer.TenantId, ct)
                        ?? throw new System.Collections.Generic.KeyNotFoundException("Customer not found for tenant.");

                    var accountTracked = await _ledgerDb.PointsAccounts
                        .FirstOrDefaultAsync(a => a.CustomerId == customer.Id, ct)
                        ?? throw new Exception("Customer has no points account.");

                    if (customerTracked.WelcomeBonusAwarded)
                    {
                        return new WelcomeBonusAwardResult(
                            customerTracked.Id,
                            false,
                            0,
                            accountTracked.Balance,
                            "already_awarded",
                            customerTracked.WelcomeBonusAwardedAt);
                    }

                    customerTracked.WelcomeBonusAwarded = true;
                    customerTracked.WelcomeBonusAwardedAt = awardedAt;

                    accountTracked.Balance += totalPoints;
                    accountTracked.UpdatedAt = awardedAt;

                    _ledgerDb.PointsTransactions.Add(new PointsTransaction
                    {
                        CustomerId = customer.Id,
                        ActorEmail = actorEmail,
                        Amount = totalPoints,
                        Reason = PointsReasons.WelcomeBonus,
                        CorrelationId = correlationId,
                        CreatedAt = awardedAt,
                        AppliedRules = appliedRules.Count > 0
                            ? JsonSerializer.SerializeToDocument(
                                appliedRules,
                                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                            : null
                    });

                    await _customersDb.SaveChangesAsync(ct);
                    await _ledgerDb.SaveChangesAsync(ct);

                    scope.Complete();
                    return new WelcomeBonusAwardResult(
                        customer.Id,
                        true,
                        totalPoints,
                        accountTracked.Balance,
                        "awarded",
                        awardedAt);
                });
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                var currentCustomer = await _customersDb.Customers
                    .AsNoTracking()
                    .FirstAsync(c => c.Id == customer.Id, ct);
                var currentAccount = await _ledgerDb.PointsAccounts
                    .AsNoTracking()
                    .FirstAsync(a => a.CustomerId == customer.Id, ct);
                return new WelcomeBonusAwardResult(
                    customer.Id,
                    false,
                    0,
                    currentAccount.Balance,
                    "already_awarded",
                    currentCustomer.WelcomeBonusAwardedAt);
            }
        }

        customer.WelcomeBonusAwarded = true;
        customer.WelcomeBonusAwardedAt = awardedAt;
        account.Balance += totalPoints;
        account.UpdatedAt = awardedAt;

        _ledgerDb.PointsTransactions.Add(new PointsTransaction
        {
            CustomerId = customer.Id,
            ActorEmail = actorEmail,
            Amount = totalPoints,
            Reason = PointsReasons.WelcomeBonus,
            CorrelationId = correlationId,
            CreatedAt = awardedAt,
            AppliedRules = appliedRules.Count > 0
                ? JsonSerializer.SerializeToDocument(
                    appliedRules,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                : null
        });

        await _customersDb.SaveChangesAsync(ct);
        await _ledgerDb.SaveChangesAsync(ct);

        return new WelcomeBonusAwardResult(
            customer.Id,
            true,
            totalPoints,
            account.Balance,
            "awarded",
            awardedAt);
    }

    private static InvoiceUpsertRequest BuildCustomerContext(Customer customer) =>
        new()
        {
            TenantId = customer.TenantId,
            InvoiceId = $"welcome-bonus:{customer.Id}",
            OccurredAt = customer.OnboardDate,
            CustomerExternalId = customer.ExternalId ?? customer.Id.ToString(),
            CustomerTier = CustomerTierCatalog.NormalizeOrDefault(customer.Tier),
            CustomerChannel = customer.BusinessSegment?.Trim(),
            CustomerRegion = customer.Address?.Region?.Trim(),
            CustomerBusinessSegment = customer.BusinessSegment?.Trim(),
            CustomerType = customer.Type?.Trim(),
            CustomerStatus = customer.Status,
            CustomerOnboardDate = customer.OnboardDate,
            Lines = new List<InvoiceLineRequest>()
        };

    private static JsonObject BuildConditionsDocument(IReadOnlyList<RuleCondition> conditions)
    {
        var root = new JsonObject();
        foreach (var condition in conditions)
        {
            if (string.Equals(condition.EntityCode, "rule", StringComparison.OrdinalIgnoreCase))
                continue;

            var key = $"{condition.EntityCode}.{condition.AttributeCode}";
            root[key] = JsonNode.Parse(condition.ValueJson.RootElement.GetRawText());
        }
        return root;
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            if (current is PostgresException pg &&
                pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }
            current = current.InnerException;
        }

        return false;
    }

    private sealed record WelcomeBonusAppliedRule(
        Guid RuleId,
        string RuleName,
        string RuleType,
        int Priority,
        DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo,
        JsonObject Conditions,
        int PointsAwarded);
}
