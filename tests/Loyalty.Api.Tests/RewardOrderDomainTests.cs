using Loyalty.Api.Modules.RewardOrders.Domain;
using Xunit;

namespace Loyalty.Api.Tests;

public class RewardOrderDomainTests
{
    [Fact]
    public void Defaults_AreSet()
    {
        var order = new RewardOrder();
        Assert.Equal(RewardOrderStatus.PendingDispatch, order.Status);
        Assert.False(order.PlacedOnBehalf);
    }
}
