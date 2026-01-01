using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Domain;

namespace Loyalty.Api.Modules.Customers.GraphQL;

/// <summary>Customer and user read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class CustomerQueries
{
    /// <summary>Returns a single customer with cached points account.</summary>
    public Task<Customer?> Customer(Guid id, [Service] ICustomerService customers) =>
        SafeExecute(() => customers.GetAsync(id));

    /// <summary>Lists customers for a tenant (admin UI).</summary>
    public Task<List<Customer>> CustomersByTenant(Guid tenantId, [Service] ICustomerService customers) =>
        SafeExecute(() => customers.ListByTenantAsync(tenantId));

    /// <summary>Lists users for a customer/outlet.</summary>
    public Task<List<User>> UsersByCustomer(Guid customerId, [Service] IUserService users) =>
        SafeExecute(() => users.ListByCustomerAsync(customerId));

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
