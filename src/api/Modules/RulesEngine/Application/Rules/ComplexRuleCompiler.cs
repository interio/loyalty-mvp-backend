using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal static class ComplexRuleCompiler
{
    public static ComplexRuleCompiledExpression Compile(
        Guid rootGroupId,
        IEnumerable<RuleConditionGroup> groups,
        IEnumerable<RuleCondition> conditions,
        ComplexRuleEntityEvaluatorRegistry? entityEvaluators = null)
    {
        entityEvaluators ??= new ComplexRuleEntityEvaluatorRegistry(null);
        var groupsById = groups.ToDictionary(g => g.Id, g => g);

        var filteredConditions = conditions
            .Where(c => !IsRuleMetadataCondition(c))
            .ToList();

        var conditionsByGroup = filteredConditions
            .GroupBy(c => c.GroupId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.SortOrder).ToList());

        var nodesByGroup = BuildNodesByGroup(groupsById, conditionsByGroup);
        var groupsWithLineScopedConditions =
            BuildLineScopedGroupSet(groupsById, filteredConditions, entityEvaluators);

        return new ComplexRuleCompiledExpression(
            rootGroupId,
            groupsById,
            nodesByGroup.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<ComplexRuleNode>)kvp.Value),
            groupsWithLineScopedConditions);
    }

    private static Dictionary<Guid, List<ComplexRuleNode>> BuildNodesByGroup(
        IReadOnlyDictionary<Guid, RuleConditionGroup> groupsById,
        IReadOnlyDictionary<Guid, List<RuleCondition>> conditionsByGroup)
    {
        var nodesByGroup = new Dictionary<Guid, List<ComplexRuleNode>>();

        foreach (var group in groupsById.Values)
        {
            var nodes = new List<ComplexRuleNode>();

            foreach (var child in groupsById.Values.Where(g => g.ParentGroupId == group.Id))
            {
                nodes.Add(new ComplexRuleNode(true, child.SortOrder, child.Id, null));
            }

            if (conditionsByGroup.TryGetValue(group.Id, out var groupConditions))
            {
                foreach (var condition in groupConditions)
                {
                    nodes.Add(new ComplexRuleNode(false, condition.SortOrder, Guid.Empty, condition));
                }
            }

            nodesByGroup[group.Id] = nodes.OrderBy(n => n.SortOrder).ToList();
        }

        return nodesByGroup;
    }

    private static HashSet<Guid> BuildLineScopedGroupSet(
        IReadOnlyDictionary<Guid, RuleConditionGroup> groupsById,
        IReadOnlyList<RuleCondition> conditions,
        ComplexRuleEntityEvaluatorRegistry entityEvaluators)
    {
        var groupsWithLineScopedConditions = new HashSet<Guid>();

        foreach (var condition in conditions)
        {
            if (entityEvaluators.IsLineScoped(condition.EntityCode))
                groupsWithLineScopedConditions.Add(condition.GroupId);
        }

        var updated = true;
        while (updated)
        {
            updated = false;
            foreach (var group in groupsById.Values)
            {
                if (!group.ParentGroupId.HasValue) continue;
                if (groupsWithLineScopedConditions.Contains(group.ParentGroupId.Value)) continue;
                if (groupsWithLineScopedConditions.Contains(group.Id))
                {
                    groupsWithLineScopedConditions.Add(group.ParentGroupId.Value);
                    updated = true;
                }
            }
        }

        return groupsWithLineScopedConditions;
    }

    private static bool IsRuleMetadataCondition(RuleCondition condition) =>
        string.Equals(condition.EntityCode, "rule", StringComparison.OrdinalIgnoreCase);
}
