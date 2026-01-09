using System.Threading.Tasks;
using Loyalty.Api.Modules.RewardOrders.Application;
using Loyalty.Api.Modules.RewardOrders.Domain;
using Xunit;

namespace Loyalty.Api.Tests;

public class RewardOrderDispatcherTests
{
    [Fact]
    public async Task StubDispatcher_Completes()
    {
        var dispatcher = new StubRewardOrderDispatcher();
        await dispatcher.EnqueueAsync(new RewardOrder());
    }
}
