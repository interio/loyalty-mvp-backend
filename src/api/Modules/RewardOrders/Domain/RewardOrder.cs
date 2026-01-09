namespace Loyalty.Api.Modules.RewardOrders.Domain;

/// <summary>Represents a redemption order placed by a customer.</summary>
public class RewardOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ActorUserId { get; set; }

    public RewardOrderStatus Status { get; set; } = RewardOrderStatus.PendingDispatch;
    public int TotalPoints { get; set; }
    public bool PlacedOnBehalf { get; set; }

    /// <summary>Optional external reference returned by the reward provider.</summary>
    public string? ProviderReference { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<RewardOrderItem> Items { get; set; } = new();
}
