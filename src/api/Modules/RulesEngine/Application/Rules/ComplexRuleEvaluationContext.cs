using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

public sealed class ComplexRuleEvaluationContext
{
    private static readonly IReadOnlyDictionary<string, JsonObject> EmptyProductAttributes =
        new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

    public ComplexRuleEvaluationContext(
        InvoiceUpsertRequest invoice,
        InvoiceLineRequest? line,
        IReadOnlyDictionary<string, JsonObject>? productAttributesBySku)
    {
        Invoice = invoice;
        Line = line;
        ProductAttributesBySku = productAttributesBySku ?? EmptyProductAttributes;
    }

    public InvoiceUpsertRequest Invoice { get; }

    public InvoiceLineRequest? Line { get; }

    public IReadOnlyDictionary<string, JsonObject> ProductAttributesBySku { get; }

    public ComplexRuleEvaluationContext WithLine(InvoiceLineRequest? line) =>
        new(Invoice, line, ProductAttributesBySku);
}
