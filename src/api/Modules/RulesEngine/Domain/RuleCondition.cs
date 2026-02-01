using HotChocolate;
using System.Text.Json;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Leaf condition node in a rule expression tree.</summary>
public class RuleCondition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string EntityCode { get; set; } = default!;
    public string AttributeCode { get; set; } = default!;
    public string Operator { get; set; } = default!;
    public JsonDocument ValueJson { get; set; } = JsonDocument.Parse("null");
    public int SortOrder { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [GraphQLIgnore] public RuleConditionGroup? Group { get; set; }
}
