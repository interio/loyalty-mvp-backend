using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal sealed class ComplexRuleCompiledExpression
{
    public ComplexRuleCompiledExpression(
        Guid rootGroupId,
        IReadOnlyDictionary<Guid, RuleConditionGroup> groups,
        IReadOnlyDictionary<Guid, IReadOnlyList<ComplexRuleNode>> nodesByGroup,
        IReadOnlySet<Guid> groupsWithLineScopedConditions)
    {
        RootGroupId = rootGroupId;
        Groups = groups;
        NodesByGroup = nodesByGroup;
        GroupsWithLineScopedConditions = groupsWithLineScopedConditions;
    }

    public Guid RootGroupId { get; }

    public IReadOnlyDictionary<Guid, RuleConditionGroup> Groups { get; }

    public IReadOnlyDictionary<Guid, IReadOnlyList<ComplexRuleNode>> NodesByGroup { get; }

    public IReadOnlySet<Guid> GroupsWithLineScopedConditions { get; }
}
