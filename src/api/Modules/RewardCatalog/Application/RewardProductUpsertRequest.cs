namespace Loyalty.Api.Modules.RewardCatalog.Application;

public class RewardProductUpsertRequest
{
    public string RewardVendor { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string? Gtin { get; set; }
    public string Name { get; set; } = default!;
    public int PointsCost { get; set; }
    public int? InventoryQuantity { get; set; }
    public Dictionary<string, object?>? Attributes { get; set; }
}

public record RewardProductUpsertBatchRequest(List<RewardProductUpsertRequest> Products);
