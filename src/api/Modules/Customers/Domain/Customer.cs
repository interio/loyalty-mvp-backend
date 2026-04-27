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

    /// <summary>Structured address details, persisted as JSON.</summary>
    public CustomerAddress? Address { get; set; }

    /// <summary>Primary contact phone number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Customer type such as bar, restaurant, hotel, or shop.</summary>
    public string? Type { get; set; }

    /// <summary>Commercial/business segment.</summary>
    public string? BusinessSegment { get; set; }

    /// <summary>Date when customer was onboarded into this platform.</summary>
    public DateTimeOffset OnboardDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Operational status code: 0 inactive, 1 active, 2 suspended.</summary>
    public int Status { get; set; } = CustomerStatusCatalog.Active;

    /// <summary>True when welcome bonus has already been awarded for this customer.</summary>
    public bool WelcomeBonusAwarded { get; set; }

    /// <summary>Timestamp when welcome bonus was awarded (UTC).</summary>
    public DateTimeOffset? WelcomeBonusAwardedAt { get; set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Users (employees) that can act on behalf of this Customer.</summary>
    public List<User> Users { get; set; } = new();

    /// <summary>Cached balance (fast reads). Source of truth remains the ledger.</summary>
    public PointsAccount? PointsAccount { get; set; }

    /// <summary>Immutable ledger of points movements for this Customer.</summary>
    public List<PointsTransaction> Transactions { get; set; } = new();
}

/// <summary>Address payload persisted in JSON for customer profile data.</summary>
public class CustomerAddress
{
    /// <summary>Street and building details.</summary>
    public string? Address { get; set; }

    /// <summary>ISO country code (for example "PL", "DE", "US").</summary>
    public string? CountryCode { get; set; }

    /// <summary>Postal or ZIP code.</summary>
    public string? PostalCode { get; set; }

    /// <summary>Administrative region/state/province.</summary>
    public string? Region { get; set; }
}
