using HotChocolate;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Selectable option for enum/select rule attributes.</summary>
public class RuleAttributeOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid AttributeId { get; set; }
    [GraphQLIgnore] public RuleAttribute? Attribute { get; set; }
    public string Value { get; set; } = default!;
    public string Label { get; set; } = default!;
}
