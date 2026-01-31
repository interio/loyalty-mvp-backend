using System.Text.Json;
using HotChocolate;
using System.ComponentModel.DataAnnotations.Schema;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Configurable points rule stored in the database.</summary>
public partial class PointsRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string RuleType { get; set; } = default!;
    [GraphQLIgnore] public JsonDocument Conditions { get; set; } = JsonDocument.Parse("{}");
    public bool Active { get; set; } = true;
    public int Priority { get; set; } = 0;
    public int RuleVersion { get; set; } = 1;
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Conditions exposed to GraphQL as key/value pairs.</summary>
    [NotMapped]
    [GraphQLName("conditions")]
    public IReadOnlyList<RuleConditionEntry> ConditionEntries => ToEntries(Conditions);
}

public record RuleConditionEntry(string Key, string? Value);

public partial class PointsRule
{
    private static IReadOnlyList<RuleConditionEntry> ToEntries(JsonDocument doc)
    {
        var list = new List<RuleConditionEntry>();
        if (doc == null) return list;

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return list;

        foreach (var prop in root.EnumerateObject())
        {
            list.Add(new RuleConditionEntry(prop.Name, ToScalarString(prop.Value)));
        }

        return list;
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
