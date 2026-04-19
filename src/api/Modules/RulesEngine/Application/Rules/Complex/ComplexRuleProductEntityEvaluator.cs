using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal sealed class ComplexRuleProductEntityEvaluator : IComplexRuleEntityEvaluator
{
    public string EntityCode => "product";

    public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.InvoiceLine;

    public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context)
    {
        if (context.Line is null)
            return false;

        var left = ResolveCoreProductValue(condition.AttributeCode, context.Line);
        if (left == null)
        {
            left = ResolveProductAttributeValue(
                condition.AttributeCode,
                context.Line.Sku,
                context.ProductAttributesBySku);
        }

        return ComplexRuleComparisonEngine.Compare(left, condition.Operator, condition.ValueJson.RootElement);
    }

    private static object? ResolveCoreProductValue(string attributeCode, InvoiceLineRequest line)
    {
        var key = ComplexRuleAttributeKey.Normalize(attributeCode);
        return key switch
        {
            "sku" => line.Sku,
            "quantity" => line.Quantity,
            "quantityinorder" => line.Quantity,
            "invoicequantityinorder" => line.Quantity,
            "netamount" => line.NetAmount,
            "distributorid" => line.DistributorId,
            _ => null
        };
    }

    private static object? ResolveProductAttributeValue(
        string attributeCode,
        string sku,
        IReadOnlyDictionary<string, JsonObject> productAttributesBySku)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return null;
        if (!productAttributesBySku.TryGetValue(sku, out var attrs) || attrs is null)
            return null;

        var normalized = ComplexRuleAttributeKey.Normalize(attributeCode);
        foreach (var kvp in attrs)
        {
            if (ComplexRuleAttributeKey.Normalize(kvp.Key) == normalized)
                return ToScalar(kvp.Value);
        }

        return null;
    }

    private static object? ToScalar(JsonNode? node)
    {
        if (node is null) return null;
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            if (v.TryGetValue<decimal>(out var d)) return d;
            if (v.TryGetValue<double>(out var db)) return db;
            if (v.TryGetValue<int>(out var i)) return i;
            if (v.TryGetValue<long>(out var l)) return l;
            if (v.TryGetValue<bool>(out var b)) return b;
        }

        return node.ToJsonString();
    }
}
