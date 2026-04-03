using HotChocolate;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Configurable points rule stored in the database.</summary>
public partial class PointsRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string RuleType { get; set; } = default!;
    public int RewardPoints { get; set; }
    public Guid? RootGroupId { get; set; }
    [GraphQLIgnore] public RuleConditionGroup? RootGroup { get; set; }
    public bool Active { get; set; } = true;
    public int Priority { get; set; } = 0;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveTo { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Conditions exposed to GraphQL as key/value pairs.</summary>
    [NotMapped]
    [GraphQLName("conditions")]
    public IReadOnlyList<RuleConditionEntry> ConditionEntries => ToEntries(RootGroup);
}

public record RuleConditionEntry(string Key, string? Value);

public partial class PointsRule
{
    private static IReadOnlyList<RuleConditionEntry> ToEntries(RuleConditionGroup? rootGroup)
    {
        if (rootGroup?.Conditions is null || rootGroup.Conditions.Count == 0)
            return Array.Empty<RuleConditionEntry>();

        return rootGroup.Conditions
            .OrderBy(c => c.SortOrder)
            .Select(c => new RuleConditionEntry(c.AttributeCode, ToScalarString(c.ValueJson.RootElement)))
            .ToList();
    }

    private static string? ToScalarString(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Number:
                return el.ToString();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return el.GetBoolean().ToString();
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                return el.GetRawText();
        }
    }
}
