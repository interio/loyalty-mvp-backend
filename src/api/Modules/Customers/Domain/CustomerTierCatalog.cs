namespace Loyalty.Api.Modules.Customers.Domain;

/// <summary>Supported customer tier values managed by Loyalty platform.</summary>
public static class CustomerTierCatalog
{
    public const string Bronze = "bronze";
    public const string Silver = "silver";
    public const string Gold = "gold";
    public const string Platinum = "platinum";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Bronze,
        Silver,
        Gold,
        Platinum
    };

    public static string NormalizeOrDefault(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? Bronze : trimmed.ToLowerInvariant();
    }

    public static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Allowed.Contains(value);
}
