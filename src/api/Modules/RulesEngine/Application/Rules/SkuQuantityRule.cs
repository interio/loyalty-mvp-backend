using Loyalty.Api.Modules.RulesEngine.Application.Invoices;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

/// <summary>SKU quantity rule: buy X quantity of each configured SKU, get Y points per SKU step.</summary>
public class SkuQuantityRule : IInvoicePointsRule
{
    private readonly IReadOnlyList<string> _skus;
    private readonly HashSet<string> _skuSet;
    private readonly decimal _quantityStep;
    private readonly int _rewardPoints;

    /// <summary>Create a SKU quantity rule.</summary>
    public SkuQuantityRule(string sku, decimal quantityStep, int rewardPoints)
        : this(ToSingleSkuList(sku), quantityStep, rewardPoints)
    {
    }

    /// <summary>Create a SKU quantity rule for multiple SKUs.</summary>
    public SkuQuantityRule(IEnumerable<string> skus, decimal quantityStep, int rewardPoints)
    {
        if (skus is null) throw new ArgumentNullException(nameof(skus));

        var normalizedSkus = skus
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(s => s!)
            .ToList();

        if (normalizedSkus.Count == 0)
            throw new ArgumentException("At least one sku is required.", nameof(skus));

        if (quantityStep <= 0) throw new ArgumentOutOfRangeException(nameof(quantityStep), "Quantity step must be > 0.");
        if (rewardPoints <= 0) throw new ArgumentOutOfRangeException(nameof(rewardPoints), "Reward points must be > 0.");

        _skus = normalizedSkus;
        _skuSet = new HashSet<string>(normalizedSkus, StringComparer.OrdinalIgnoreCase);
        _quantityStep = quantityStep;
        _rewardPoints = rewardPoints;
    }

    private static IReadOnlyList<string> ToSingleSkuList(string sku)
    {
        if (sku is null) throw new ArgumentNullException(nameof(sku));
        return new[] { sku };
    }

    /// <inheritdoc />
    public string Name => $"Sku([{string.Join("|", _skus)}],{_quantityStep}->{_rewardPoints})";

    /// <inheritdoc />
    public int CalculatePoints(InvoiceUpsertRequest invoice)
    {
        var qtyBySku = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in invoice.Lines)
        {
            var sku = line.Sku?.Trim();
            if (string.IsNullOrWhiteSpace(sku) || !_skuSet.Contains(sku))
                continue;

            qtyBySku.TryGetValue(sku, out var existingQty);
            qtyBySku[sku] = existingQty + line.Quantity;
        }

        var totalPoints = 0;
        foreach (var qty in qtyBySku.Values)
        {
            if (qty <= 0)
                continue;

            var steps = (int)Math.Floor(qty / _quantityStep);
            totalPoints += steps * _rewardPoints;
        }

        return totalPoints;
    }
}
