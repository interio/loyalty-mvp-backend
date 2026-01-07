using Loyalty.Api.Modules.RulesEngine.Application.Invoices;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

/// <summary>Wraps a rule with its metadata for audit logging.</summary>
public sealed class MetadataInvoicePointsRule : IInvoicePointsRuleWithMetadata
{
    private readonly IInvoicePointsRule _inner;

    public MetadataInvoicePointsRule(IInvoicePointsRule inner, InvoiceRuleMetadata metadata)
    {
        _inner = inner;
        Metadata = metadata;
    }

    public InvoiceRuleMetadata Metadata { get; }

    public string Name => _inner.Name;

    public int CalculatePoints(InvoiceUpsertRequest invoice) => _inner.CalculatePoints(invoice);
}
