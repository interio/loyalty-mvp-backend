namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal static class ComplexRuleAwardMode
{
    public const string Static = "static";
    public const string PerCurrency = "per_currency";

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" => Static,
            "static" => Static,
            "per_currency" => PerCurrency,
            "percurrency" => PerCurrency,
            "per-currency" => PerCurrency,
            _ => normalized
        };
    }
}

internal static class ComplexRuleAwardMetadata
{
    public const string AwardMode = "awardMode";
    public const string PointsPerCurrencyPoints = "pointsPerCurrencyPoints";
    public const string PointsPerCurrencyAmount = "pointsPerCurrencyAmount";
}

internal readonly record struct ComplexRuleAwardConfig(
    string Mode,
    int StaticPoints,
    int PointsPerCurrencyPoints,
    decimal PointsPerCurrencyAmount)
{
    public bool IsStatic => string.Equals(Mode, ComplexRuleAwardMode.Static, StringComparison.OrdinalIgnoreCase);
    public bool IsPerCurrency => string.Equals(Mode, ComplexRuleAwardMode.PerCurrency, StringComparison.OrdinalIgnoreCase);

    public static ComplexRuleAwardConfig CreateStatic(int points) =>
        new(ComplexRuleAwardMode.Static, points, 0, 0);

    public static ComplexRuleAwardConfig CreatePerCurrency(int points, decimal amount) =>
        new(ComplexRuleAwardMode.PerCurrency, 0, points, amount);
}
