using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Nodes;
using HotChocolate;

namespace Loyalty.Api.Modules.Products.Domain;

/// <summary>
/// Product definition scoped to tenant and distributor (ERP master). Supports extensible attributes for future rules.
/// </summary>
public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant that owns this product catalog entry.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Distributor identifier from ERP (tenant-scoped).</summary>
    public Guid DistributorId { get; set; }

    [GraphQLIgnore] public Distributor? Distributor { get; set; }

    /// <summary>Distributor display name for admin readability.</summary>
    [NotMapped]
    [GraphQLName("distributorDisplayName")]
    public string? DistributorDisplayName => Distributor?.DisplayName;

    /// <summary>ERP SKU/code (may overlap across distributors).</summary>
    public string Sku { get; set; } = default!;

    /// <summary>Global Trade Item Number (can be null for non-GTIN items).</summary>
    public string? Gtin { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Unit cost in source currency (deprecated, optional).</summary>
    [GraphQLDeprecated("Deprecated. This field is optional and will be removed in a future API version.")]
    public decimal? Cost { get; set; }

    /// <summary>Extensible attributes for future rule conditions (stored as JSON).</summary>
    [GraphQLIgnore] public JsonObject Attributes { get; set; } = new();

    /// <summary>Attributes exposed to GraphQL as key/value pairs (values serialized to string).</summary>
    [NotMapped]
    [GraphQLName("attributes")]
    public List<ProductAttributeEntry> AttributeEntries =>
        Attributes.Select(kvp => new ProductAttributeEntry(kvp.Key, ToScalarString(kvp.Value))).ToList();

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

public record ProductAttributeEntry(string Key, string? Value);
