namespace Loyalty.Api.Modules.LoyaltyLedger.Domain;

/// <summary>
/// Well-known reason codes for points transactions to avoid magic strings.
/// </summary>
public static class PointsReasons
{
    /// <summary>Redeem points for a reward.</summary>
    public const string RewardRedeem = "reward_redeem";

    /// <summary>Earn points from an invoice or sale.</summary>
    public const string InvoiceEarn = "invoice_earn";

    /// <summary>Manual adjustment by operations.</summary>
    public const string ManualAdjustment = "manual_adjustment";
}
