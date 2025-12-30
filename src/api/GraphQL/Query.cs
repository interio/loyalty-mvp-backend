using HotChocolate;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Domain;

namespace Loyalty.Api.GraphQL;

/// <summary>
/// GraphQL read operations for the frontend/admin UI.
/// </summary>
public class Query
{
    /// <summary>
    /// Returns a single customer/outlet with its cached points account balance.
    /// </summary>
    public Task<Customer?> Customer(Guid id, [Service] ICustomerService customers) =>
        SafeExecute(() => customers.GetAsync(id));

    /// <summary>
    /// Lists customers for a given tenant.
    /// Useful for admin UI screens.
    /// </summary>
    public Task<List<Customer>> CustomersByTenant(Guid tenantId, [Service] ICustomerService customers) =>
        SafeExecute(() => customers.ListByTenantAsync(tenantId));

    /// <summary>
    /// Lists users (employees) for a customer/outlet.
    /// </summary>
    public Task<List<User>> UsersByCustomer(Guid customerId, [Service] IUserService users) =>
        SafeExecute(() => users.ListByCustomerAsync(customerId));

    /// <summary>
    /// Returns last N ledger entries for a customer/outlet (immutable points history).
    /// </summary>
    public Task<List<PointsTransaction>> CustomerTransactions(Guid customerId, [Service] ILedgerService ledger) =>
        SafeExecute(() => ledger.GetTransactionsForCustomerAsync(customerId));

    /// <summary>
    /// Lists tenants (admin convenience; can be removed once auth/tenant scoping is implemented).
    /// </summary>
    public Task<List<Tenant>> Tenants([Service] ITenantService tenants) =>
        SafeExecute(() => tenants.ListAsync());

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
