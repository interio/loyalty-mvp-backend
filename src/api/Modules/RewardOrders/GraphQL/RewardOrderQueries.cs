using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RewardOrders.Application;
using Loyalty.Api.Modules.RewardOrders.Domain;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.RewardOrders.GraphQL;

/// <summary>Reward orders read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class RewardOrderQueries
{
    public Task<List<RewardOrder>> RewardOrdersByCustomer(Guid customerId, [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.ListByCustomerAsync(customerId));

    public Task<List<RewardOrder>> RewardOrdersByTenant(Guid tenantId, [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.ListByTenantAsync(tenantId));

    public Task<RewardOrderConnection> RewardOrdersByTenantPage(
        Guid tenantId,
        int page,
        int pageSize,
        [Service] RewardOrderService orders) =>
        SafeExecute(async () =>
        {
            var result = await orders.ListByTenantPageAsync(tenantId, page, pageSize);
            return new RewardOrderConnection(
                result.Items,
                new PageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        });

    public Task<RewardOrderCursorConnection> RewardOrdersByTenantCursor(
        Guid tenantId,
        int take,
        string? after,
        [Service] RewardOrderService orders) =>
        SafeExecute(async () =>
        {
            var result = await orders.ListByTenantCursorAsync(tenantId, take, after);
            return new RewardOrderCursorConnection(result.Items, new CursorPageInfo(result.EndCursor, result.HasNextPage));
        });

    public Task<List<RewardOrderSummary>> RewardOrdersByTenantRange(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.ListSummaryByTenantRangeAsync(tenantId, from, to));

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

public record RewardOrderConnection(IReadOnlyList<RewardOrder> Nodes, PageInfo PageInfo);

public record RewardOrderCursorConnection(IReadOnlyList<RewardOrder> Nodes, CursorPageInfo PageInfo);
