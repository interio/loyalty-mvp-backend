using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal readonly record struct ComplexRuleNode(
    bool IsGroup,
    int SortOrder,
    Guid GroupId,
    RuleCondition? Condition);
