namespace Loyalty.Api.Modules.RewardOrders.Domain;

/// <summary>Snapshot of a reward product line in a redemption order.</summary>
public class RewardOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RewardOrderId { get; set; }
    public Guid RewardProductId { get; set; }

    public string RewardVendor { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;

    public int Quantity { get; set; }
    public int PointsCost { get; set; }
    public int TotalPoints { get; set; }
}
