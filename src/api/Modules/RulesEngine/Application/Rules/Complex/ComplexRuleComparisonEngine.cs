using System.Text.Json;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

internal static class ComplexRuleComparisonEngine
{
    public static bool Compare(object? left, string? op, JsonElement right)
    {
        if (left == null) return false;
        var oper = (op ?? string.Empty).Trim().ToLowerInvariant();
        return oper switch
        {
            "eq" => AreEqual(left, right),
            "neq" => !AreEqual(left, right),
            "contains" => Contains(left, right),
            "in" => InSet(left, right),
            "nin" => !InSet(left, right),
            "gt" => GreaterThan(left, right),
            "gte" => GreaterThanOrEqual(left, right),
            "lt" => LessThan(left, right),
            "lte" => LessThanOrEqual(left, right),
            _ => false
        };
    }

    private static bool AreEqual(object left, JsonElement right)
    {
        if (TryGetDecimal(left, out var leftNum) && TryGetDecimal(right, out var rightNum))
            return leftNum == rightNum;

        if (TryGetDateTime(left, out var leftDate) && TryGetDateTime(right, out var rightDate))
            return leftDate == rightDate;

        if (left is bool leftBool && TryGetBool(right, out var rightBool))
            return leftBool == rightBool;

        var leftStr = left.ToString() ?? string.Empty;
        var rightStr = GetString(right);
        return string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(object left, JsonElement right)
    {
        var leftStr = left.ToString() ?? string.Empty;
        var rightStr = GetString(right);
        if (string.IsNullOrWhiteSpace(rightStr)) return false;
        return leftStr.IndexOf(rightStr, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool InSet(object left, JsonElement right)
    {
        if (right.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in right.EnumerateArray())
            {
                if (AreEqual(left, item)) return true;
            }
            return false;
        }

        return AreEqual(left, right);
    }

    private static bool GreaterThan(object left, JsonElement right)
    {
        if (TryGetDecimal(left, out var leftNum) && TryGetDecimal(right, out var rightNum))
            return leftNum > rightNum;

        if (TryGetDateTime(left, out var leftDate) && TryGetDateTime(right, out var rightDate))
            return leftDate > rightDate;

        return false;
    }

    private static bool GreaterThanOrEqual(object left, JsonElement right)
    {
        if (TryGetDecimal(left, out var leftNum) && TryGetDecimal(right, out var rightNum))
            return leftNum >= rightNum;

        if (TryGetDateTime(left, out var leftDate) && TryGetDateTime(right, out var rightDate))
            return leftDate >= rightDate;

        return false;
    }

    private static bool LessThan(object left, JsonElement right)
    {
        if (TryGetDecimal(left, out var leftNum) && TryGetDecimal(right, out var rightNum))
            return leftNum < rightNum;

        if (TryGetDateTime(left, out var leftDate) && TryGetDateTime(right, out var rightDate))
            return leftDate < rightDate;

        return false;
    }

    private static bool LessThanOrEqual(object left, JsonElement right)
    {
        if (TryGetDecimal(left, out var leftNum) && TryGetDecimal(right, out var rightNum))
            return leftNum <= rightNum;

        if (TryGetDateTime(left, out var leftDate) && TryGetDateTime(right, out var rightDate))
            return leftDate <= rightDate;

        return false;
    }

    private static bool TryGetDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case decimal dec:
                result = dec;
                return true;
            case double dbl:
                result = (decimal)dbl;
                return true;
            case float flt:
                result = (decimal)flt;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case string s when decimal.TryParse(s, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static bool TryGetDecimal(JsonElement element, out decimal result)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out result))
            return true;

        if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), out result))
            return true;

        result = 0;
        return false;
    }

    private static bool TryGetBool(JsonElement element, out bool result)
    {
        if (element.ValueKind == JsonValueKind.True)
        {
            result = true;
            return true;
        }
        if (element.ValueKind == JsonValueKind.False)
        {
            result = false;
            return true;
        }
        if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out result))
            return true;

        result = false;
        return false;
    }

    private static bool TryGetDateTime(object value, out DateTimeOffset result)
    {
        switch (value)
        {
            case DateTimeOffset dto:
                result = dto;
                return true;
            case DateTime dt:
                result = new DateTimeOffset(dt);
                return true;
            case string s when DateTimeOffset.TryParse(s, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool TryGetDateTime(JsonElement element, out DateTimeOffset result)
    {
        if (element.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(element.GetString(), out result))
            return true;

        result = default;
        return false;
    }

    private static string GetString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.ToString()
        };
    }
}
