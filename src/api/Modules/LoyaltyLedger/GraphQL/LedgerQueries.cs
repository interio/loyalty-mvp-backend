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
    public Task<List<PointsTransaction>> CustomerTransactions(Guid customerId, [Service] ILedgerService ledger) =>
        SafeExecute(() => ledger.GetTransactionsForCustomerAsync(customerId));

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
