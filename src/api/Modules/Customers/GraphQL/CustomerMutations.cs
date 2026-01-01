using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Domain;

namespace Loyalty.Api.Modules.Customers.GraphQL;

/// <summary>Customer and user mutations.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class CustomerMutations
{
    /// <summary>Creates a customer/outlet and seeds its points account.</summary>
    public Task<Customer> CreateCustomer(CreateCustomerInput input, [Service] ICustomerService customers) =>
        SafeExecute(() => customers.CreateAsync(new CreateCustomerCommand(input.TenantId, input.Name, input.ContactEmail, input.ExternalId)));

    /// <summary>Creates a user under a customer/outlet.</summary>
    public Task<User> CreateUser(CreateUserInput input, [Service] IUserService users) =>
        SafeExecute(() => users.CreateAsync(new CreateUserCommand(input.TenantId, input.CustomerId, input.Email, input.Role, input.ExternalId)));

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

/// <summary>Input for creating a customer/outlet.</summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="Name">Customer name.</param>
/// <param name="ContactEmail">Optional contact email.</param>
/// <param name="ExternalId">Optional ERP/CRM customer ID.</param>
public record CreateCustomerInput(Guid TenantId, string Name, string? ContactEmail, string? ExternalId);

/// <summary>Input for creating a user/employee.</summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CustomerId">Customer/outlet identifier.</param>
/// <param name="Email">User email.</param>
/// <param name="Role">Optional role.</param>
/// <param name="ExternalId">Optional upstream ID.</param>
public record CreateUserInput(Guid TenantId, Guid CustomerId, string Email, string? Role, string? ExternalId);
