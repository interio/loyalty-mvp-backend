using HotChocolate;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Boolean group node in a rule expression tree.</summary>
public class RuleConditionGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    public Guid? ParentGroupId { get; set; }
    public string Logic { get; set; } = "AND";
    public int SortOrder { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [GraphQLIgnore] public PointsRule? Rule { get; set; }
    [GraphQLIgnore] public RuleConditionGroup? ParentGroup { get; set; }
    [GraphQLIgnore] public ICollection<RuleConditionGroup> ChildGroups { get; set; } = new List<RuleConditionGroup>();
    [GraphQLIgnore] public ICollection<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();
}
