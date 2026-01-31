using HotChocolate;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Allowed operator for a rule attribute.</summary>
[GraphQLName("Operator")]
public class RuleAttributeOperator
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid AttributeId { get; set; }
    [GraphQLIgnore] public RuleAttribute? Attribute { get; set; }
    public string Operator { get; set; } = default!;
}
