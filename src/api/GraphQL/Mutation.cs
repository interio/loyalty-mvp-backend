using Loyalty.Api.Data;
using Loyalty.Api.Domain;
using Microsoft.EntityFrameworkCore;

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
    public async Task<Tenant> CreateTenant(CreateTenantInput input, [Service] LoyaltyDbContext db)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new Exception("Tenant name is required.");

        var tenant = new Tenant { Name = name };
        db.Tenants.Add(tenant);

        await db.SaveChangesAsync();
        return tenant;
    }

    /// <summary>
    /// Creates a customer/outlet and its points account (balance=0).
    /// </summary>
    public async Task<Customer> CreateCustomer(CreateCustomerInput input, [Service] LoyaltyDbContext db)
    {
        // Tenant must exist.
        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == input.TenantId);
        if (!tenantExists)
            throw new Exception("Tenant not found.");

        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new Exception("Customer name is required.");

        var contactEmail = input.ContactEmail?.Trim();
        var externalId = input.ExternalId?.Trim();

        var customer = new Customer
        {
            TenantId = input.TenantId,
            Name = name,
            ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail,
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId
        };

        db.Customers.Add(customer);

        // Create cached balance record immediately.
        db.PointsAccounts.Add(new PointsAccount
        {
            CustomerId = customer.Id,
            Balance = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
        return customer;
    }

    /// <summary>
    /// Creates a user/employee under a customer/outlet.
    /// </summary>
    public async Task<User> CreateUser(CreateUserInput input, [Service] LoyaltyDbContext db)
    {
        var email = input.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            throw new Exception("Email is required.");

        // Validate tenant and customer existence and consistency (customer must belong to tenant).
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == input.CustomerId);
        if (customer is null)
            throw new Exception("Customer not found.");
        if (customer.TenantId != input.TenantId)
            throw new Exception("Customer does not belong to the specified tenant.");

        var role = input.Role?.Trim();
        var externalId = input.ExternalId?.Trim();

        var user = new User
        {
            TenantId = input.TenantId,
            CustomerId = input.CustomerId,
            Email = email,
            Role = string.IsNullOrWhiteSpace(role) ? null : role,
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Redeems points for a customer/outlet. Creates an immutable ledger entry and updates cached balance.
    /// </summary>
    /// <remarks>
    /// Ledger immutability is enforced in DB via trigger (UPDATE/DELETE blocked).
    /// Corrections must be compensating inserts.
    /// </remarks>
    public async Task<PointsAccount> RedeemPoints(RedeemPointsInput input, [Service] LoyaltyDbContext db)
    {
        if (input.Amount <= 0)
            throw new Exception("Amount must be greater than 0.");

        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new Exception("Reason is required.");

        // Validate actor user belongs to the same customer.
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.ActorUserId);
        if (actor is null)
            throw new Exception("Actor user not found.");
        if (actor.CustomerId != input.CustomerId)
            throw new Exception("Actor user does not belong to the specified customer.");

        var account = await db.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == input.CustomerId);
        if (account is null)
            throw new Exception("Customer has no points account.");

        // Optional idempotency.
        var corr = input.CorrelationId?.Trim();
        if (!string.IsNullOrWhiteSpace(corr))
        {
            var exists = await db.PointsTransactions.AnyAsync(t =>
                t.CustomerId == input.CustomerId && t.CorrelationId == corr);

            if (exists)
                return account;
        }
        else
        {
            corr = null;
        }

        // Debit points as a negative ledger entry.
        var delta = -input.Amount;
        var newBalance = account.Balance + delta;

        if (newBalance < 0)
            throw new Exception("Insufficient points.");

        db.PointsTransactions.Add(new PointsTransaction
        {
            CustomerId = input.CustomerId,
            ActorUserId = input.ActorUserId,
            Amount = delta,
            Reason = reason,
            CorrelationId = corr
        });

        account.Balance = newBalance;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return account;
    }
}