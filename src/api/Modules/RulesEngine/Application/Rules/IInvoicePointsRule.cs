using Loyalty.Api.Modules.RulesEngine.Application.Invoices;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

/// <summary>Rule that computes points from an invoice.</summary>
public interface IInvoicePointsRule
{
    /// <summary>Rule name for diagnostics.</summary>
    string Name { get; }

    /// <summary>Compute points for the invoice (>=0).</summary>
    int CalculatePoints(InvoiceUpsertRequest invoice);
}

/// <summary>Rule provider (DB-backed).</summary>
public interface IInvoicePointsRuleProvider
{
    /// <summary>Fetch active rules for a tenant.</summary>
    Task<IReadOnlyList<IInvoicePointsRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default);
}
