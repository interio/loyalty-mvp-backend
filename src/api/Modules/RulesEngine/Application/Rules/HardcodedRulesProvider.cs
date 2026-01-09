using Loyalty.Api.Modules.RulesEngine.Application.Invoices;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

/// <summary>Fallback rule provider for local demos/tests.</summary>
public class HardcodedRulesProvider : IInvoicePointsRuleProvider
{
    public Task<IReadOnlyList<IInvoicePointsRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<IInvoicePointsRule>>(GetRules());

    public IReadOnlyList<IInvoicePointsRule> GetRules() => new IInvoicePointsRule[]
    {
        new SpendRule(100m, 10),
        new SkuQuantityRule("BEER-HEINEKEN-BTL-24PK", 4m, 25)
    };
}
