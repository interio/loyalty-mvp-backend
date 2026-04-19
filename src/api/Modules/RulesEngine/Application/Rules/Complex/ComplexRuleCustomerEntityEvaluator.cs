using System;
using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal sealed class ComplexRuleCustomerEntityEvaluator : IComplexRuleEntityEvaluator
{
    public string EntityCode => "customer";

    public ComplexRuleEntityScope Scope => ComplexRuleEntityScope.Invoice;

    public bool Evaluate(RuleCondition condition, ComplexRuleEvaluationContext context)
    {
        var key = ComplexRuleAttributeKey.Normalize(condition.AttributeCode);
        if (key is "channel" or "customerchannel")
            return EvaluateChannelCondition(condition, context.Invoice.CustomerChannel);

        var left = ResolveCustomerValue(key, context);
        return ComplexRuleComparisonEngine.Compare(left, condition.Operator, condition.ValueJson.RootElement);
    }

    private static object? ResolveCustomerValue(string normalizedAttributeCode, ComplexRuleEvaluationContext context)
    {
        return normalizedAttributeCode switch
        {
            "tier" or "customertier" => context.Invoice.CustomerTier,
            "externalid" => context.Invoice.CustomerExternalId,
            "region" or "customerregion" => context.Invoice.CustomerRegion,
            _ => null
        };
    }

    private static bool EvaluateChannelCondition(RuleCondition condition, string? customerChannel)
    {
        if (string.IsNullOrWhiteSpace(customerChannel))
            return false;

        var op = (condition.Operator ?? string.Empty).Trim().ToLowerInvariant();
        return op switch
        {
            "eq" => MatchesLike(customerChannel, condition.ValueJson.RootElement),
            "neq" => !MatchesLike(customerChannel, condition.ValueJson.RootElement),
            "in" => InSetLike(customerChannel, condition.ValueJson.RootElement),
            "nin" => !InSetLike(customerChannel, condition.ValueJson.RootElement),
            _ => ComplexRuleComparisonEngine.Compare(customerChannel, condition.Operator, condition.ValueJson.RootElement)
        };
    }

    private static bool InSetLike(string left, System.Text.Json.JsonElement right)
    {
        if (right.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in right.EnumerateArray())
            {
                if (MatchesLike(left, item))
                    return true;
            }
            return false;
        }

        return MatchesLike(left, right);
    }

    private static bool MatchesLike(string left, System.Text.Json.JsonElement right)
    {
        var raw = right.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => right.GetString(),
            System.Text.Json.JsonValueKind.Number => right.ToString(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            _ => right.ToString()
        };

        var needle = (raw ?? string.Empty).Trim();
        if (needle.Length == 0)
            return false;

        if (needle.StartsWith('%') || needle.EndsWith('%'))
            needle = needle.Trim('%').Trim();

        if (needle.Length == 0)
            return false;

        return left.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
