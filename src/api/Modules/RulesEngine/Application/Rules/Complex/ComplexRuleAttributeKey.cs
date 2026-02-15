namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal static class ComplexRuleAttributeKey
{
    public static string Normalize(string? value) =>
        new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
