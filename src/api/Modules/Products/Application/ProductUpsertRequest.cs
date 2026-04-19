using System.ComponentModel.DataAnnotations;

namespace Loyalty.Api.Modules.Products.Application;

/// <summary>Single product upsert payload.</summary>
public class ProductUpsertRequest
{
    [Required] public Guid TenantId { get; set; }
    /// <summary>
    /// Distributor id (legacy input). Optional when <see cref="DistributorName"/> is provided.
    /// </summary>
    public Guid DistributorId { get; set; }

    /// <summary>
    /// Distributor name (preferred input for ERP/admin integrations). Tenant-scoped.
    /// </summary>
    public string? DistributorName { get; set; }
    [Required] public string Sku { get; set; } = default!;
    [Required] public string Name { get; set; } = default!;
    public string? Gtin { get; set; }
    /// <summary>Deprecated. Optional and scheduled for removal in a future API version.</summary>
    public decimal? Cost { get; set; }

    /// <summary>Extensible attributes (JSON object) for future rules.</summary>
    public Dictionary<string, object?>? Attributes { get; set; }
}

/// <summary>Batch upsert request.</summary>
public class ProductUpsertBatchRequest
{
    [Required] public List<ProductUpsertRequest> Products { get; set; } = new();
}
