using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal sealed class ComplexRuleDistributorEntityEvaluator : IComplexRuleEntityEvaluator
{
    public string EntityCode => "distributor";

    public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.InvoiceLine;

    public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context)
    {
        if (context.Line is null)
            return false;

        var left = ResolveDistributorValue(condition.AttributeCode, context.Line);
        return ComplexRuleComparisonEngine.Compare(left, condition.Operator, condition.ValueJson.RootElement);
    }

    private static object? ResolveDistributorValue(string attributeCode, InvoiceLineRequest line)
    {
        var key = ComplexRuleAttributeKey.Normalize(attributeCode);
        return key switch
        {
            "id" => line.DistributorId,
            "distributorid" => line.DistributorId,
            _ => null
        };
    }
}
