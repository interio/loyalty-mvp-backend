using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RewardOrders.Application;
using Loyalty.Api.Modules.RewardOrders.Domain;

namespace Loyalty.Api.Modules.RewardOrders.GraphQL;

/// <summary>Reward orders write operations.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class RewardOrderMutations
{
    public Task<RewardOrder> PlaceRewardOrder(PlaceRewardOrderRequest request, [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.PlaceOrderAsync(request, false));

    public Task<RewardOrder> PlaceRewardOrderOnBehalf(PlaceRewardOrderOnBehalfRequest request, [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.PlaceOrderAsync(new PlaceRewardOrderRequest
        {
            TenantId = request.TenantId,
            CustomerId = request.CustomerId,
            ActorUserId = request.AdminUserId,
            Items = request.Items
        }, true));

    private static async Task<T> SafeExecute<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }
}
