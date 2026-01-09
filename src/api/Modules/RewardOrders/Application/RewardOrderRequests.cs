namespace Loyalty.Api.Modules.RewardOrders.Application;

public record RewardOrderLineRequest(Guid RewardProductId, int Quantity);

public class PlaceRewardOrderRequest
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ActorUserId { get; set; }
    public List<RewardOrderLineRequest> Items { get; set; } = new();
}

public class PlaceRewardOrderOnBehalfRequest
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid AdminUserId { get; set; }
    public List<RewardOrderLineRequest> Items { get; set; } = new();
}
