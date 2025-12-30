using HotChocolate;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Domain;

namespace Loyalty.Api.GraphQL;

/// <summary>
/// Input for creating a tenant (client/organization).
/// </summary>
public record CreateTenantInput(string Name);

/// <summary>
/// Input for creating a customer/outlet (bar/restaurant/shop).
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="Name">Customer name (e.g. "Blue Fox Bar").</param>
/// <param name="ContactEmail">Optional point-of-contact email for the outlet.</param>
/// <param name="ExternalId">Optional ERP/CRM customer ID.</param>
public record CreateCustomerInput(Guid TenantId, string Name, string? ContactEmail, string? ExternalId);

/// <summary>
/// Input for creating a user/employee under a customer/outlet.
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="CustomerId">Customer/outlet identifier.</param>
/// <param name="Email">User email (login identifier).</param>
/// <param name="Role">Optional role string (e.g., Owner/Employee/Admin).</param>
/// <param name="ExternalId">Optional upstream user ID.</param>
public record CreateUserInput(Guid TenantId, Guid CustomerId, string Email, string? Role, string? ExternalId);

/// <summary>
/// Input for redeeming points for a reward/loyalty product.
/// This is a frontend-driven operation (GraphQL).
/// </summary>
/// <param name="CustomerId">Customer/outlet whose balance is debited.</param>
/// <param name="ActorUserId">User who initiated the redemption.</param>
/// <param name="Amount">Positive amount to redeem; stored as negative ledger entry.</param>
/// <param name="Reason">Reason label (e.g. "reward_redeem").</param>
/// <param name="CorrelationId">Optional idempotency key (e.g. redemption order id).</param>
public record RedeemPointsInput(Guid CustomerId, Guid ActorUserId, int Amount, string Reason, string? CorrelationId);

/// <summary>
/// GraphQL mutations for frontend/admin operations (NOT ERP ingestion).
/// </summary>
public class Mutation
{
    /// <summary>
    /// Creates a tenant record.
    /// </summary>
    public Task<Tenant> CreateTenant(CreateTenantInput input, [Service] ITenantService tenants) =>
        SafeExecute(() => tenants.CreateAsync(input.Name));

    /// <summary>
    /// Creates a customer/outlet and its points account (balance=0).
    /// </summary>
    public Task<Customer> CreateCustomer(CreateCustomerInput input, [Service] ICustomerService customers) =>
        SafeExecute(() => customers.CreateAsync(new CreateCustomerCommand(input.TenantId, input.Name, input.ContactEmail, input.ExternalId)));

    /// <summary>
    /// Creates a user/employee under a customer/outlet.
    /// </summary>
    public Task<User> CreateUser(CreateUserInput input, [Service] IUserService users) =>
        SafeExecute(() => users.CreateAsync(new CreateUserCommand(input.TenantId, input.CustomerId, input.Email, input.Role, input.ExternalId)));

    /// <summary>
    /// Redeems points for a customer/outlet. Creates an immutable ledger entry and updates cached balance.
    /// </summary>
    /// <remarks>
    /// Ledger immutability is enforced in DB via trigger (UPDATE/DELETE blocked).
    /// Corrections must be compensating inserts.
    /// </remarks>
    public Task<PointsAccount> RedeemPoints(RedeemPointsInput input, [Service] ILedgerService ledger) =>
        SafeExecute(() => ledger.RedeemAsync(new RedeemPointsCommand(input.CustomerId, input.ActorUserId, input.Amount, input.Reason, input.CorrelationId)));

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
