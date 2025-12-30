using Loyalty.Api.Modules.Customers.Domain;

namespace Loyalty.Api.Modules.Tenants.Domain;

/// <summary>
/// Tenant boundary. Even if MVP runs single-tenant, this keeps the model future-proof and safer.
/// </summary>
public class Tenant
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant display name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Customers/outlets belonging to this tenant.</summary>
    public List<Customer> Customers { get; set; } = new();

    /// <summary>All users under this tenant (convenience navigation).</summary>
    public List<User> Users { get; set; } = new();
}
