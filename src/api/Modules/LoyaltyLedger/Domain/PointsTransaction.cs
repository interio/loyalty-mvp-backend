using System.Text.Json;
using HotChocolate;
using Loyalty.Api.Modules.Customers.Domain;

namespace Loyalty.Api.Modules.LoyaltyLedger.Domain;

/// <summary>
/// Append-only ledger entry representing a points movement for a Customer.
/// This table must be immutable: do NOT update/delete rows. Use compensating transactions instead.
/// </summary>
public class PointsTransaction
{
    /// <summary>Unique identifier of the ledger entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The Customer/outlet whose balance is affected.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Customer navigation.</summary>
    public Customer Customer { get; set; } = default!;

    /// <summary>
    /// Optional actor user who initiated the action (UI redemption, manual adjustment, etc.).
    /// ERP-driven points may not always provide user identity, so this can be null.
    /// </summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Optional actor navigation.</summary>
    public User? ActorUser { get; set; }

    /// <summary>
    /// Signed delta. Positive = earn/credit. Negative = redeem/debit.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// Human-readable reason (e.g. "invoice_points", "reward_redeem", "manual_adjustment").
    /// </summary>
    public string Reason { get; set; } = "manual";

    /// <summary>
    /// Optional idempotency/correlation key (e.g. ERP invoice id, redemption id, message id).
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Applied rules snapshot for audit/traceability.</summary>
    [GraphQLIgnore]
    public JsonDocument? AppliedRules { get; set; }
}
