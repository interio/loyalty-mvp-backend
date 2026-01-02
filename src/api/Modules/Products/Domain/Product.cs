using System.Text.Json.Nodes;

namespace Loyalty.Api.Modules.Products.Domain;

/// <summary>
/// Product definition scoped to a distributor (ERP master). Supports extensible attributes for future rules.
/// </summary>
public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Distributor (or tenant-level) identifier from ERP.</summary>
    public Guid DistributorId { get; set; }

    /// <summary>ERP SKU/code (may overlap across distributors).</summary>
    public string Sku { get; set; } = default!;

    /// <summary>Global Trade Item Number (can be null for non-GTIN items).</summary>
    public string? Gtin { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Unit cost in source currency (for rule calculations).</summary>
    public decimal Cost { get; set; }

    /// <summary>Extensible attributes for future rule conditions (stored as JSON).</summary>
    public JsonObject Attributes { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
