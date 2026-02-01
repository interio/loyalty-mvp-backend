using System.Text.Json;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

/// <summary>Complex rule with nested condition groups.</summary>
public class ComplexRule : IInvoicePointsRule, IInvoicePointsRuleWithProductAttributes
{
    private readonly Guid _ruleId;
    private readonly Guid _rootGroupId;
    private readonly int _rewardPoints;
    private readonly Dictionary<Guid, RuleConditionGroup> _groups;
    private readonly Dictionary<Guid, List<RuleCondition>> _conditionsByGroup;
    private readonly Dictionary<Guid, List<Node>> _nodesByGroup;
    private readonly HashSet<Guid> _groupsWithProductConditions;
    private IReadOnlyDictionary<string, JsonObject> _productAttributesBySku =
        new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

    private readonly record struct Node(bool IsGroup, int SortOrder, Guid GroupId, RuleCondition? Condition);

    public ComplexRule(
        Guid ruleId,
        Guid rootGroupId,
        int rewardPoints,
        IEnumerable<RuleConditionGroup> groups,
        IEnumerable<RuleCondition> conditions)
    {
        _ruleId = ruleId;
        _rootGroupId = rootGroupId;
        _rewardPoints = rewardPoints;
        _groups = groups.ToDictionary(g => g.Id, g => g);

        var filteredConditions = conditions
            .Where(c => !IsRuleMetadataCondition(c))
            .ToList();

        _conditionsByGroup = filteredConditions
            .GroupBy(c => c.GroupId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.SortOrder).ToList());

        _nodesByGroup = BuildNodesByGroup(filteredConditions);
        _groupsWithProductConditions = BuildProductGroupSet(filteredConditions);
    }

    public string Name => $"ComplexRule({_ruleId})";

    public int CalculatePoints(InvoiceUpsertRequest invoice)
    {
        if (_rewardPoints <= 0) return 0;
        if (!_nodesByGroup.ContainsKey(_rootGroupId)) return 0;
        return EvaluateGroup(_rootGroupId, invoice, null) ? _rewardPoints : 0;
    }

    public void SetProductAttributes(IReadOnlyDictionary<string, JsonObject> attributesBySku)
    {
        _productAttributesBySku = attributesBySku is null
            ? new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonObject>(attributesBySku, StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<Guid, List<Node>> BuildNodesByGroup(IReadOnlyList<RuleCondition> conditions)
    {
        var nodesByGroup = new Dictionary<Guid, List<Node>>();

        foreach (var group in _groups.Values)
        {
            var nodes = new List<Node>();

            foreach (var child in _groups.Values.Where(g => g.ParentGroupId == group.Id))
            {
                nodes.Add(new Node(true, child.SortOrder, child.Id, null));
            }

            if (_conditionsByGroup.TryGetValue(group.Id, out var groupConditions))
            {
                foreach (var condition in groupConditions)
                {
                    nodes.Add(new Node(false, condition.SortOrder, Guid.Empty, condition));
                }
            }

            nodesByGroup[group.Id] = nodes.OrderBy(n => n.SortOrder).ToList();
        }

        return nodesByGroup;
    }

    private HashSet<Guid> BuildProductGroupSet(IReadOnlyList<RuleCondition> conditions)
    {
        var groupsWithProduct = new HashSet<Guid>();

        foreach (var condition in conditions)
        {
            if (IsProductCondition(condition))
            {
                groupsWithProduct.Add(condition.GroupId);
            }
        }

        var updated = true;
        while (updated)
        {
            updated = false;
            foreach (var group in _groups.Values)
            {
                if (!group.ParentGroupId.HasValue) continue;
                if (groupsWithProduct.Contains(group.ParentGroupId.Value)) continue;
                if (groupsWithProduct.Contains(group.Id))
                {
                    groupsWithProduct.Add(group.ParentGroupId.Value);
                    updated = true;
                }
            }
        }

        return groupsWithProduct;
    }

    private bool EvaluateGroup(Guid groupId, InvoiceUpsertRequest invoice, InvoiceLineRequest? line)
    {
        if (!_groups.TryGetValue(groupId, out var group)) return false;
        if (!_nodesByGroup.TryGetValue(groupId, out var nodes) || nodes.Count == 0) return false;

        var logic = group.Logic?.Trim().ToUpperInvariant() == "OR" ? "OR" : "AND";

        if (logic == "OR")
        {
            foreach (var node in nodes)
            {
                if (EvaluateNode(node, invoice, line)) return true;
            }
            return false;
        }

        if (line != null)
        {
            foreach (var node in nodes)
            {
                if (!EvaluateNode(node, invoice, line)) return false;
            }
            return true;
        }

        var productNodes = nodes.Where(NodeHasProduct).ToList();
        foreach (var node in nodes.Except(productNodes))
        {
            if (!EvaluateNode(node, invoice, null)) return false;
        }

        if (productNodes.Count == 0) return true;

        return invoice.Lines.Any(lineItem => productNodes.All(node => EvaluateNode(node, invoice, lineItem)));
    }

    private bool EvaluateNode(Node node, InvoiceUpsertRequest invoice, InvoiceLineRequest? line)
    {
        if (node.IsGroup)
        {
            return EvaluateGroup(node.GroupId, invoice, line);
        }

        return node.Condition != null && EvaluateCondition(node.Condition, invoice, line);
    }

    private bool EvaluateCondition(RuleCondition condition, InvoiceUpsertRequest invoice, InvoiceLineRequest? line)
    {
        var entity = (condition.EntityCode ?? string.Empty).Trim().ToLowerInvariant();
        if (entity == "product")
        {
            if (line == null)
            {
                return invoice.Lines.Any(lineItem => EvaluateCondition(condition, invoice, lineItem));
            }

            var left = GetProductValue(condition.AttributeCode, line);
            if (left == null)
            {
                left = GetProductAttributeValue(condition.AttributeCode, line.Sku);
            }
            return Compare(left, condition.Operator, condition.ValueJson.RootElement);
        }

        if (entity == "invoice")
        {
            var left = GetInvoiceValue(condition.AttributeCode, invoice);
            return Compare(left, condition.Operator, condition.ValueJson.RootElement);
        }

        return false;
    }

    private bool NodeHasProduct(Node node)
    {
        if (node.IsGroup) return _groupsWithProductConditions.Contains(node.GroupId);
        return node.Condition != null && IsProductCondition(node.Condition);
    }

    private static bool IsProductCondition(RuleCondition condition) =>
        string.Equals(condition.EntityCode, "product", StringComparison.OrdinalIgnoreCase);

    private static bool IsRuleMetadataCondition(RuleCondition condition) =>
        string.Equals(condition.EntityCode, "rule", StringComparison.OrdinalIgnoreCase);

    private static object? GetInvoiceValue(string attributeCode, InvoiceUpsertRequest invoice)
    {
        var key = Normalize(attributeCode);
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

    private static object? GetProductValue(string attributeCode, InvoiceLineRequest line)
    {
        var key = Normalize(attributeCode);
        return key switch
        {
            "sku" => line.Sku,
            "quantity" => line.Quantity,
            "netamount" => line.NetAmount,
            _ => null
        };
    }

    private object? GetProductAttributeValue(string attributeCode, string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        if (!_productAttributesBySku.TryGetValue(sku, out var attrs) || attrs is null) return null;

        var normalized = Normalize(attributeCode);
        foreach (var kvp in attrs)
        {
            if (Normalize(kvp.Key) == normalized)
            {
                return ToScalar(kvp.Value);
            }
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

    private static string Normalize(string value) =>
        new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static bool Compare(object? left, string? op, JsonElement right)
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
