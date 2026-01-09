namespace Loyalty.Api.Modules.RewardOrders.Domain;

public enum RewardOrderStatus
{
    PendingDispatch = 0,
    Dispatched = 1,
    Failed = 2,
    Cancelled = 3
}
