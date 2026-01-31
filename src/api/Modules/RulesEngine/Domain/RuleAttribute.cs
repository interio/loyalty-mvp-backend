using HotChocolate;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Attribute metadata for a rule entity.</summary>
public class RuleAttribute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid EntityId { get; set; }
    [GraphQLIgnore] public RuleEntity? Entity { get; set; }
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string ValueType { get; set; } = default!;
    public bool IsMultiValue { get; set; } = false;
    public bool IsQueryable { get; set; } = true;
    public string UiControl { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
