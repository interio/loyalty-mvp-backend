using Loyalty.Api.Modules.RewardOrders.Domain;

namespace Loyalty.Api.Modules.RewardOrders.Application;

/// <summary>Stub dispatcher for sending orders to the reward provider.</summary>
public interface IRewardOrderDispatcher
{
    Task EnqueueAsync(RewardOrder order, CancellationToken ct = default);
}

public class StubRewardOrderDispatcher : IRewardOrderDispatcher
{
    public Task EnqueueAsync(RewardOrder order, CancellationToken ct = default) => Task.CompletedTask;
}
