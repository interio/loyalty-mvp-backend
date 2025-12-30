using Loyalty.Api.Modules.Integration.Application.Invoices;

namespace Loyalty.Api.Modules.Integration.Application.Rules;

/// <summary>Spend X get Y points rule.</summary>
public class SpendRule : IInvoicePointsRule
{
    private readonly decimal _spendStep;
    private readonly int _rewardPoints;

    /// <summary>Create a spend-based rule.</summary>
    public SpendRule(decimal spendStep, int rewardPoints)
    {
        if (spendStep <= 0) throw new ArgumentOutOfRangeException(nameof(spendStep), "Spend step must be > 0.");
        if (rewardPoints <= 0) throw new ArgumentOutOfRangeException(nameof(rewardPoints), "Reward points must be > 0.");
        _spendStep = spendStep;
        _rewardPoints = rewardPoints;
    }

    /// <inheritdoc />
    public string Name => $"Spend({_spendStep}->{_rewardPoints})";

    /// <inheritdoc />
    public int CalculatePoints(InvoiceUpsertRequest invoice)
    {
        var total = invoice.Lines.Sum(l => l.NetAmount);
        if (total <= 0) return 0;
        var steps = (int)Math.Floor(total / _spendStep);
        return steps * _rewardPoints;
    }
}
