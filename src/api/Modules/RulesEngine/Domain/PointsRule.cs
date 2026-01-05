using System.Text.Json;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Configurable points rule stored in the database.</summary>
public class PointsRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string RuleType { get; set; } = default!;
    public JsonDocument Conditions { get; set; } = JsonDocument.Parse("{}");
    public bool Active { get; set; } = true;
    public int Priority { get; set; } = 0;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
