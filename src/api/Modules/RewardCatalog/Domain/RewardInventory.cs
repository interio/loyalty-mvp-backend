namespace Loyalty.Api.Modules.RewardCatalog.Domain;

/// <summary>
/// Inventory snapshot for a reward product. Stored separately to avoid frequent updates to the catalog row.
/// </summary>
public class RewardInventory
{
    public Guid RewardProductId { get; set; }

    /// <summary>Available quantity for redemption.</summary>
    public int AvailableQuantity { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSyncedAt { get; set; }
}
