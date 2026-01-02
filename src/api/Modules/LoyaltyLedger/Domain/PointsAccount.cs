using Loyalty.Api.Modules.Customers.Domain;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Api.Modules.LoyaltyLedger.Domain;

/// <summary>
/// Cached balance for fast reads. The immutable ledger is PointsTransaction.
/// </summary>
public class PointsAccount
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Customer that owns this points account (one-to-one).</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Customer navigation.</summary>
    public Customer Customer { get; set; } = default!;

    /// <summary>
    /// Cached balance (derived from ledger in principle).
    /// </summary>
    public long Balance { get; set; } = 0;

    /// <summary>When the cached balance was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Concurrency token for balance updates.</summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
