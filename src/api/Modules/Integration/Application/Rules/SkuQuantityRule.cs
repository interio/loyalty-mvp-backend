using Loyalty.Api.Modules.Integration.Application.Invoices;

namespace Loyalty.Api.Modules.Integration.Application.Rules;

/// <summary>SKU quantity rule: buy X quantity of a SKU, get Y points.</summary>
public class SkuQuantityRule : IInvoicePointsRule
{
    private readonly string _sku;
    private readonly decimal _quantityStep;
    private readonly int _rewardPoints;

    /// <summary>Create a SKU quantity rule.</summary>
    public SkuQuantityRule(string sku, decimal quantityStep, int rewardPoints)
    {
        _sku = sku ?? throw new ArgumentNullException(nameof(sku));
        if (quantityStep <= 0) throw new ArgumentOutOfRangeException(nameof(quantityStep), "Quantity step must be > 0.");
        if (rewardPoints <= 0) throw new ArgumentOutOfRangeException(nameof(rewardPoints), "Reward points must be > 0.");
        _quantityStep = quantityStep;
        _rewardPoints = rewardPoints;
    }

    /// <inheritdoc />
    public string Name => $"Sku({_sku},{_quantityStep}->{_rewardPoints})";

    /// <inheritdoc />
    public int CalculatePoints(InvoiceUpsertRequest invoice)
    {
        var qty = invoice.Lines
            .Where(l => string.Equals(l.Sku, _sku, StringComparison.OrdinalIgnoreCase))
            .Sum(l => l.Quantity);

        if (qty <= 0) return 0;
        var steps = (int)Math.Floor(qty / _quantityStep);
        return steps * _rewardPoints;
    }
}
