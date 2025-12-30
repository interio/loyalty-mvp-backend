using Loyalty.Api.Modules.Customers.Domain;

namespace Loyalty.Api.Modules.Customers.Application;

/// <summary>Read-only contract for customer lookups (cross-module safe).</summary>
public interface ICustomerLookup
{
    /// <summary>Retrieve a customer by id.</summary>
    Task<Customer?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Check if a customer belongs to the specified tenant.</summary>
    Task<bool> BelongsToTenantAsync(Guid customerId, Guid tenantId, CancellationToken ct = default);
}

/// <summary>Read-only contract for user lookups (cross-module safe).</summary>
public interface IUserLookup
{
    /// <summary>Retrieve a user by id.</summary>
    Task<User?> GetAsync(Guid id, CancellationToken ct = default);
}
