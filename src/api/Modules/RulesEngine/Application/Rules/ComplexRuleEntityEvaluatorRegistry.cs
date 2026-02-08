namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal sealed class ComplexRuleEntityEvaluatorRegistry
{
    private readonly Dictionary<string, IComplexRuleEntityEvaluator> _evaluators =
        new(StringComparer.OrdinalIgnoreCase);

    public ComplexRuleEntityEvaluatorRegistry(IEnumerable<IComplexRuleEntityEvaluator>? entityEvaluators)
    {
        foreach (var evaluator in ComplexRuleEntityEvaluatorDefaults.MergeWithDefaults(entityEvaluators))
        {
            if (string.IsNullOrWhiteSpace(evaluator.EntityCode))
                continue;

            _evaluators[evaluator.EntityCode.Trim()] = evaluator;
        }
    }

    public bool TryGet(string? entityCode, out IComplexRuleEntityEvaluator evaluator)
    {
        evaluator = default!;
        if (string.IsNullOrWhiteSpace(entityCode))
            return false;

        if (_evaluators.TryGetValue(entityCode.Trim(), out var resolved))
        {
            evaluator = resolved;
            return true;
        }

        return false;
    }

    public bool IsLineScoped(string? entityCode) =>
        TryGet(entityCode, out var evaluator) &&
        evaluator.Scope == ComplexRuleEntityScope.InvoiceLine;
}
