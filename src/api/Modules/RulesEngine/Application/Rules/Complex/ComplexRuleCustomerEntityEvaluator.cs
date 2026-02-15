using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal sealed class ComplexRuleCustomerEntityEvaluator : IComplexRuleEntityEvaluator
{
    public string EntityCode => "customer";

    public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.Invoice;

    public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context)
    {
        var left = ResolveCustomerValue(condition.AttributeCode, context);
        return ComplexRuleComparisonEngine.Compare(left, condition.Operator, condition.ValueJson.RootElement);
    }

    private static object? ResolveCustomerValue(string attributeCode, ComplexRuleEvaluationContext context)
    {
        var key = ComplexRuleAttributeKey.Normalize(attributeCode);
        return key switch
        {
            "tier" => context.Invoice.CustomerTier,
            "customertier" => context.Invoice.CustomerTier,
            "externalid" => context.Invoice.CustomerExternalId,
            _ => null
        };
    }
}
