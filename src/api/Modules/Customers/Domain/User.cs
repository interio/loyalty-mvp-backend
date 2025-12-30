using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Tenants.Domain;

namespace Loyalty.Api.Modules.Customers.Domain;

/// <summary>
/// A human user/employee who can place orders and redeem rewards on behalf of a Customer.
/// </summary>
public class User
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant boundary (prevents cross-tenant mixing).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning tenant navigation.</summary>
    public Tenant Tenant { get; set; } = default!;

    /// <summary>The Customer/outlet this user belongs to.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Customer navigation.</summary>
    public Customer Customer { get; set; } = default!;

    /// <summary>User email (typically used as login identifier).</summary>
    public string Email { get; set; } = default!;

    /// <summary>Optional upstream identifier (ERP/IdP/HR).</summary>
    public string? ExternalId { get; set; }

    /// <summary>Optional role (e.g., "Owner", "Employee", "Admin"). MVP can keep as free text.</summary>
    public string? Role { get; set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Ledger entries this user initiated (optional attribution).</summary>
    public List<PointsTransaction> InitiatedTransactions { get; set; } = new();
}
