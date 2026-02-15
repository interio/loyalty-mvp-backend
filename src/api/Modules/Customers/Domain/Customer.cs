using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.Tenants.Domain;

namespace Loyalty.Api.Modules.Customers.Domain;

/// <summary>
/// A business customer/outlet (bar/restaurant/shop) participating in the loyalty program.
/// Points balance and ledger are maintained at this level.
/// </summary>
public class Customer
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant that owns this customer record.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Owning tenant navigation.</summary>
    public Tenant Tenant { get; set; } = default!;

    /// <summary>Display name (e.g. "Blue Fox Bar").</summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Customer-level point of contact email (not necessarily a login).
    /// Useful for operational communication and data matching.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// External identifier from upstream systems (ERP/CRM/customer master).
    /// Strongly recommended for matching inbound ERP documents.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Loyalty tier assigned by this platform (not ERP): bronze, silver, gold, platinum.
    /// </summary>
    public string Tier { get; set; } = CustomerTierCatalog.Bronze;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Users (employees) that can act on behalf of this Customer.</summary>
    public List<User> Users { get; set; } = new();

    /// <summary>Cached balance (fast reads). Source of truth remains the ledger.</summary>
    public PointsAccount? PointsAccount { get; set; }

    /// <summary>Immutable ledger of points movements for this Customer.</summary>
    public List<PointsTransaction> Transactions { get; set; } = new();
}
