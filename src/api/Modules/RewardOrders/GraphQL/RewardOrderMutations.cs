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

    public Task<RewardOrder> UpdateRewardOrderStatus(UpdateRewardOrderStatusInput input, [Service] RewardOrderService orders) =>
        SafeExecute(() => orders.UpdateStatusAsync(input.TenantId, input.OrderId, ParseStatus(input.Status)));

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

public record UpdateRewardOrderStatusInput(Guid TenantId, Guid OrderId, string Status);

internal static RewardOrderStatus ParseStatus(string status)
{
    if (string.IsNullOrWhiteSpace(status))
        throw new ArgumentException("Status is required.");

    var normalized = status.Trim();
    if (Enum.TryParse<RewardOrderStatus>(normalized, ignoreCase: true, out var parsed))
        return parsed;

    var stripped = normalized.Replace("_", string.Empty).Replace("-", string.Empty);
    if (Enum.TryParse<RewardOrderStatus>(stripped, ignoreCase: true, out parsed))
        return parsed;

    throw new ArgumentException($"Unknown status '{status}'.");
}
