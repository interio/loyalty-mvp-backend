using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

public interface IComplexRuleEntityEvaluator
{
    string EntityCode { get; }

    ComplexRuleEntityScope Scope { get; }

    bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context);
}
