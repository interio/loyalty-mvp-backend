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

    /// <summary>Pages customers for a tenant (admin UI).</summary>
    public Task<CustomerConnection> CustomersByTenantPage(
        Guid tenantId,
        int page,
        int pageSize,
        [Service] ICustomerService customers) =>
        SafeExecute(async () =>
        {
            var result = await customers.ListByTenantPageAsync(tenantId, page, pageSize);
            return new CustomerConnection(
                result.Items,
                new CustomerPageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        });

    /// <summary>Searches customers for a tenant using Postgres full-text search.</summary>
    public Task<List<Customer>> CustomersByTenantSearch(Guid tenantId, string search, [Service] ICustomerService customers) =>
        SafeExecute(() => customers.SearchByTenantAsync(tenantId, search));

    /// <summary>Lists users for a tenant (admin UI).</summary>
    public Task<List<User>> UsersByTenant(Guid tenantId, [Service] IUserService users) =>
        SafeExecute(() => users.ListByTenantAsync(tenantId));

    /// <summary>Pages users for a tenant (admin UI).</summary>
    public Task<UserConnection> UsersByTenantPage(
        Guid tenantId,
        int page,
        int pageSize,
        [Service] IUserService users) =>
        SafeExecute(async () =>
        {
            var result = await users.ListByTenantPageAsync(tenantId, page, pageSize);
            return new UserConnection(
                result.Items,
                new UserPageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        });

    /// <summary>Searches users for a tenant.</summary>
    public Task<List<User>> UsersByTenantSearch(Guid tenantId, string search, [Service] IUserService users) =>
        SafeExecute(() => users.SearchByTenantAsync(tenantId, search));

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

public record CustomerConnection(IReadOnlyList<Customer> Nodes, CustomerPageInfo PageInfo);

public record CustomerPageInfo(int TotalCount, int Page, int PageSize, int TotalPages);

public record UserConnection(IReadOnlyList<User> Nodes, UserPageInfo PageInfo);

public record UserPageInfo(int TotalCount, int Page, int PageSize, int TotalPages);
