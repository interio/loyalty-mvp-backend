using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;

namespace Loyalty.Api.Modules.LoyaltyLedger.GraphQL;

/// <summary>Ledger mutations.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class LedgerMutations
{
    /// <summary>Redeems points for a customer/outlet.</summary>
    public Task<PointsAccount> RedeemPoints(RedeemPointsInput input, [Service] ILedgerService ledger) =>
        SafeExecute(() => ledger.RedeemAsync(new RedeemPointsCommand(input.CustomerId, input.ActorUserId, input.Amount, input.Reason, input.CorrelationId)));

    /// <summary>Manually adjusts points (positive or negative) for a customer/outlet.</summary>
    public Task<PointsAccount> ManualAdjustPoints(ManualAdjustPointsInput input, [Service] ILedgerService ledger) =>
        SafeExecute(() => ledger.AdjustAsync(new ManualAdjustPointsCommand(
            input.CustomerId,
            input.ActorUserId,
            input.ActorEmail,
            input.Comment,
            input.Amount,
            PointsReasons.ManualAdjustment,
            input.CorrelationId)));

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

/// <summary>Input for redeeming points.</summary>
/// <param name="CustomerId">Customer/outlet whose balance is debited.</param>
/// <param name="ActorUserId">User who initiated the redemption.</param>
/// <param name="Amount">Positive amount to redeem; stored as negative ledger entry.</param>
/// <param name="Reason">Reason code (e.g., reward_redeem).</param>
/// <param name="CorrelationId">Optional idempotency key.</param>
public record RedeemPointsInput(Guid CustomerId, Guid ActorUserId, int Amount, string Reason, string? CorrelationId);

/// <summary>Input for manually adjusting points.</summary>
/// <param name="CustomerId">Customer/outlet whose balance is adjusted.</param>
/// <param name="ActorUserId">Optional user performing the adjustment.</param>
/// <param name="Amount">Signed amount (positive to credit, negative to debit). Cannot be 0.</param>
/// <param name="CorrelationId">Optional idempotency key.</param>
public record ManualAdjustPointsInput(
    Guid CustomerId,
    Guid? ActorUserId,
    string? ActorEmail,
    string? Comment,
    int Amount,
    string? CorrelationId);
