namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal static class ComplexRuleEntityEvaluatorDefaults
{
    public static IReadOnlyList<IComplexRuleEntityEvaluator> Create() =>
        new IComplexRuleEntityEvaluator[]
        {
            new ComplexRuleInvoiceEntityEvaluator(),
            new ComplexRuleCustomerEntityEvaluator(),
            new ComplexRuleDistributorEntityEvaluator(),
            new ComplexRuleProductEntityEvaluator()
        };

    public static IEnumerable<IComplexRuleEntityEvaluator> MergeWithDefaults(
        IEnumerable<IComplexRuleEntityEvaluator>? entityEvaluators)
    {
        foreach (var evaluator in Create())
            yield return evaluator;

        if (entityEvaluators is null)
            yield break;

        foreach (var evaluator in entityEvaluators)
            yield return evaluator;
    }
}
