namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>Shared operator catalog for rule attributes.</summary>
public static class RuleOperatorCatalog
{
    public static readonly IReadOnlyList<RuleOperatorInfo> Items = new[]
    {
        new RuleOperatorInfo("eq", "Equals"),
        new RuleOperatorInfo("neq", "Does not equal"),
        new RuleOperatorInfo("in", "Is in list"),
        new RuleOperatorInfo("nin", "Is not in list"),
        new RuleOperatorInfo("contains", "Contains"),
        new RuleOperatorInfo("gt", "Greater than"),
        new RuleOperatorInfo("gte", "Greater than or equal"),
        new RuleOperatorInfo("lt", "Less than"),
        new RuleOperatorInfo("lte", "Less than or equal"),
    };

    public static readonly ISet<string> AllowedValues =
        new HashSet<string>(Items.Select(o => o.Value), StringComparer.OrdinalIgnoreCase);
}

public record RuleOperatorInfo(string Value, string Label);
