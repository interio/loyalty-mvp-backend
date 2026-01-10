using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RewardOrders.Application;
using Loyalty.Api.Modules.RewardOrders.Domain;

namespace Loyalty.Api.Modules.RewardOrders.GraphQL;

/// <summary>Reward orders read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class RewardOrderQueries
{
    public Task<List<RewardOrder>> RewardOrdersByCustomer(Guid customerId, [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.ListByCustomerAsync(customerId));

    public Task<List<RewardOrder>> RewardOrdersByTenant(Guid tenantId, [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.ListByTenantAsync(tenantId));

    public Task<RewardOrder?> RewardOrder(Guid tenantId, Guid id, [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.GetByIdAsync(tenantId, id));

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
