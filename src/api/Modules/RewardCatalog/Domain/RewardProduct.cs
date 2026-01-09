using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
using HotChocolate;

namespace Loyalty.Api.Modules.RewardCatalog.Domain;

/// <summary>
/// Reward product definition scoped to a reward vendor.
/// </summary>
public class RewardProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>External reward vendor identifier.</summary>
    public string RewardVendor { get; set; } = default!;

    /// <summary>Vendor SKU/code.</summary>
    public string Sku { get; set; } = default!;

    /// <summary>Global Trade Item Number (optional).</summary>
    public string? Gtin { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Points cost per unit.</summary>
    public int PointsCost { get; set; }

    /// <summary>Extensible attributes for vendor-specific metadata.</summary>
    [GraphQLIgnore] public JsonObject Attributes { get; set; } = new();

    /// <summary>Attributes exposed to GraphQL as key/value pairs (values serialized to string).</summary>
    [NotMapped]
    [GraphQLName("attributes")]
    public List<RewardProductAttributeEntry> AttributeEntries =>
        Attributes.Select(kvp => new RewardProductAttributeEntry(kvp.Key, ToScalarString(kvp.Value))).ToList();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    private static string? ToScalarString(JsonNode? node)
    {
        if (node is null) return null;
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            if (v.TryGetValue<decimal>(out var d)) return d.ToString();
            if (v.TryGetValue<double>(out var db)) return db.ToString();
            if (v.TryGetValue<int>(out var i)) return i.ToString();
            if (v.TryGetValue<long>(out var l)) return l.ToString();
            if (v.TryGetValue<bool>(out var b)) return b.ToString();
        }
        return node.ToJsonString();
    }
}

public record RewardProductAttributeEntry(string Key, string? Value);
