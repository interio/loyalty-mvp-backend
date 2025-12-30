namespace Loyalty.Api.Domain;

/// <summary>
/// Cached balance for fast reads. The immutable ledger is PointsTransaction.
/// </summary>
public class PointsAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Customer that owns this points account (one-to-one).</summary>
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    /// <summary>
    /// Cached balance (derived from ledger in principle).
    /// </summary>
    public long Balance { get; set; } = 0;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}