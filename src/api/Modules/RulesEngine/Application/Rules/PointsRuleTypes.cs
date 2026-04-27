namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

public static class PointsRuleTypes
{
    public const string Spend = "spend";
    public const string SkuQuantity = "sku_quantity";
    public const string ComplexRule = "complex_rule";
    public const string WelcomeBonus = "welcome_bonus";

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
