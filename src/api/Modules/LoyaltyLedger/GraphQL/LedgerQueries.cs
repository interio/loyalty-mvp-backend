using System.Text.Json;
using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;

namespace Loyalty.Api.Modules.LoyaltyLedger.GraphQL;

/// <summary>Ledger read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class LedgerQueries
{
    /// <summary>Returns recent ledger entries for a customer/outlet.</summary>
    public Task<List<PointsTransactionDto>> CustomerTransactions(Guid customerId, [Service] ILedgerService ledger) =>
        SafeExecute(async () =>
        {
            var rows = await ledger.GetTransactionsForCustomerAsync(customerId);
            return rows.Select(Map).ToList();
        });

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

    private static PointsTransactionDto Map(PointsTransaction tx) =>
        new(
            tx.Id,
            tx.CustomerId,
            tx.ActorUserId,
            tx.Amount,
            tx.Reason,
            tx.CorrelationId,
            tx.CreatedAt,
            tx.AppliedRules == null ? null : tx.AppliedRules.RootElement.GetRawText());
}

/// <summary>Ledger transaction with optional applied rules snapshot.</summary>
public record PointsTransactionDto(
    Guid Id,
    Guid CustomerId,
    Guid? ActorUserId,
    int Amount,
    string Reason,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    string? AppliedRulesJson);
