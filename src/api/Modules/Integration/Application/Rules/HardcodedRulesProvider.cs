using Loyalty.Api.Modules.Integration.Application.Invoices;

namespace Loyalty.Api.Modules.Integration.Application.Rules;

/// <summary>
/// Temporary hardcoded rules. Replace with DB-configured rules managed by Admin UI in the future.
/// </summary>
public class HardcodedRulesProvider : IInvoicePointsRuleProvider
{
    private readonly IReadOnlyList<IInvoicePointsRule> _rules;

    /// <summary>Create the provider with static rules.</summary>
    public HardcodedRulesProvider()
    {
        _rules = new List<IInvoicePointsRule>
        {
            new SpendRule(100m, 10),
            new SkuQuantityRule("BEER-HEINEKEN-BTL-24PK", 4m, 25)
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<IInvoicePointsRule> GetRules() => _rules;
}

/// <summary>Contract for rule providers (future DB-backed implementation).</summary>
public interface IInvoicePointsRuleProvider
{
    /// <summary>Return the set of rules to evaluate for an invoice.</summary>
    IReadOnlyList<IInvoicePointsRule> GetRules();
}
