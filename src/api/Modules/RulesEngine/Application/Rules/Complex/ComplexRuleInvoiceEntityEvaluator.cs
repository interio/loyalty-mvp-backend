using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal sealed class ComplexRuleInvoiceEntityEvaluator : IComplexRuleEntityEvaluator
{
    public string EntityCode => "invoice";

    public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.Invoice;

    public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context)
    {
        var left = ResolveInvoiceValue(condition.AttributeCode, context.Invoice);
        return ComplexRuleComparisonEngine.Compare(left, condition.Operator, condition.ValueJson.RootElement);
    }

    private static object? ResolveInvoiceValue(string attributeCode, InvoiceUpsertRequest invoice)
    {
        var key = ComplexRuleAttributeKey.Normalize(attributeCode);
        return key switch
        {
            "currency" => invoice.Currency,
            "totalamount" => invoice.Lines.Sum(l => l.NetAmount),
            "totalnetamount" => invoice.Lines.Sum(l => l.NetAmount),
            "invoiceid" => invoice.InvoiceId,
            "occurredat" => invoice.OccurredAt,
            "customerexternalid" => invoice.CustomerExternalId,
            "linescount" => invoice.Lines.Count,
            "tenantid" => invoice.TenantId,
            "actoremail" => invoice.ActorEmail,
            "actorexternalid" => invoice.ActorExternalId,
            _ => null
        };
    }
}
