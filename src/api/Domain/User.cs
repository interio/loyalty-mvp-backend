namespace Loyalty.Api.Domain;

/// <summary>
/// A human user/employee who can place orders and redeem rewards on behalf of a Customer.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant boundary (prevents cross-tenant mixing).</summary>
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    /// <summary>The Customer/outlet this user belongs to.</summary>
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    /// <summary>User email (typically used as login identifier).</summary>
    public string Email { get; set; } = default!;

    /// <summary>Optional upstream identifier (ERP/IdP/HR).</summary>
    public string? ExternalId { get; set; }

    /// <summary>Optional role (e.g., "Owner", "Employee", "Admin"). MVP can keep as free text.</summary>
    public string? Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Ledger entries this user initiated (optional attribution).</summary>
    public List<PointsTransaction> InitiatedTransactions { get; set; } = new();
}