using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

/// <summary>Loads invoice points rules from the database.</summary>
public class DatabaseInvoicePointsRuleProvider : IInvoicePointsRuleProvider
{
    private readonly IntegrationDbContext _db;
    private readonly ILogger<DatabaseInvoicePointsRuleProvider> _logger;

    public DatabaseInvoicePointsRuleProvider(IntegrationDbContext db, ILogger<DatabaseInvoicePointsRuleProvider> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IInvoicePointsRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await _db.PointsRules
            .AsNoTracking()
            .Where(r =>
                r.TenantId == tenantId &&
                r.Active &&
                r.EffectiveFrom <= now &&
                (r.EffectiveTo == null || r.EffectiveTo >= now))
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Array.Empty<IInvoicePointsRule>();

        var ruleIds = rows.Select(r => r.Id).ToList();
        var groups = await _db.RuleConditionGroups
            .AsNoTracking()
            .Where(g => ruleIds.Contains(g.RuleId))
            .ToListAsync(ct);

        var groupIds = groups.Select(g => g.Id).ToList();
        var conditions = groupIds.Count == 0
            ? new List<RuleCondition>()
            : await _db.RuleConditions
                .AsNoTracking()
                .Where(c => groupIds.Contains(c.GroupId))
                .ToListAsync(ct);

        var groupsByRuleId = groups
            .GroupBy(g => g.RuleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RuleConditionGroup>)g.ToList());

        var conditionsByGroupId = conditions
            .GroupBy(c => c.GroupId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RuleCondition>)g.ToList());

        var rules = new List<IInvoicePointsRule>();
        foreach (var row in rows)
        {
            try
            {
                groupsByRuleId.TryGetValue(row.Id, out var ruleGroups);
                ruleGroups ??= Array.Empty<RuleConditionGroup>();

                var ruleConditions = ruleGroups
                    .SelectMany(g => conditionsByGroupId.TryGetValue(g.Id, out var list) ? list : Array.Empty<RuleCondition>())
                    .ToList();

                var rootConditions = row.RootGroupId.HasValue &&
                                     conditionsByGroupId.TryGetValue(row.RootGroupId.Value, out var rootList)
                    ? rootList
                    : Array.Empty<RuleCondition>();

                var parsed = ParseRule(row, ruleGroups, ruleConditions, rootConditions);
                if (parsed != null)
                {
                    var metadata = new InvoiceRuleMetadata(
                        row.Id,
                        row.RuleVersion,
                        row.RuleType,
                        row.Priority,
                        row.Active,
                        row.EffectiveFrom,
                        row.EffectiveTo,
                        BuildConditionsDocument(rootConditions),
                        row.Name);
                    rules.Add(new MetadataInvoicePointsRule(parsed, metadata));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse rule {RuleId} ({RuleType})", row.Id, row.RuleType);
            }
        }

        return rules;
    }

    private static IInvoicePointsRule? ParseRule(
        PointsRule rule,
        IReadOnlyList<RuleConditionGroup> groups,
        IReadOnlyList<RuleCondition> conditions,
        IReadOnlyList<RuleCondition> rootConditions)
    {
        var type = rule.RuleType.Trim().ToLowerInvariant();

        switch (type)
        {
            case "spend":
            case "spend_rule":
                {
                    var conditionMap = BuildConditionMap(rootConditions);
                    if (conditionMap.Count == 0)
                        return null;
                    var spendStep = GetDecimal(conditionMap, "spendStep");
                    var rewardPoints = GetInt(conditionMap, "rewardPoints");
                    if (spendStep <= 0 || rewardPoints <= 0)
                        throw new ArgumentException("Spend rule requires spendStep > 0 and rewardPoints > 0.");
                    return new SpendRule(spendStep, rewardPoints);
                }
            case "sku_quantity":
            case "sku_quantity_rule":
                {
                    var conditionMap = BuildConditionMap(rootConditions);
                    if (conditionMap.Count == 0)
                        return null;
                    var sku = GetString(conditionMap, "sku");
                    var quantityStep = GetDecimal(conditionMap, "quantityStep");
                    var rewardPoints = GetInt(conditionMap, "rewardPoints");
                    if (string.IsNullOrWhiteSpace(sku) || quantityStep <= 0 || rewardPoints <= 0)
                        throw new ArgumentException("Sku quantity rule requires sku, quantityStep > 0, rewardPoints > 0.");
                    return new SkuQuantityRule(sku, quantityStep, rewardPoints);
                }
            case "complex_rule":
            case "complex":
                {
                    if (!rule.RootGroupId.HasValue)
                        throw new ArgumentException("Complex rule requires a root condition group.");
                    if (groups.Count == 0)
                        throw new ArgumentException("Complex rule requires condition groups.");
                    if (!groups.Any(g => g.Id == rule.RootGroupId.Value))
                        throw new ArgumentException("Complex rule root group not found.");

                    var rewardPoints = GetRewardPoints(conditions);
                    if (rewardPoints <= 0)
                        throw new ArgumentException("Complex rule requires rewardPoints > 0.");

                    return new ComplexRule(rule.Id, rule.RootGroupId.Value, rewardPoints, groups, conditions);
                }
            default:
                throw new ArgumentException($"Unsupported rule type: {rule.RuleType}");
        }
    }

    private static IReadOnlyDictionary<string, string?> BuildConditionMap(IReadOnlyList<RuleCondition> conditions)
    {
        if (conditions.Count == 0)
            return new Dictionary<string, string?>();

        return conditions
            .Where(c => string.Equals(c.EntityCode, "rule", StringComparison.OrdinalIgnoreCase))
            .GroupBy(c => c.AttributeCode)
            .ToDictionary(
                entry => entry.Key,
                entry => ToScalarString(entry.First().ValueJson.RootElement));
    }

    private static int GetInt(IReadOnlyDictionary<string, string?> map, string name)
    {
        if (!map.TryGetValue(name, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0;
        return int.TryParse(raw, out var value) ? value : 0;
    }

    private static decimal GetDecimal(IReadOnlyDictionary<string, string?> map, string name)
    {
        if (!map.TryGetValue(name, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0;
        return decimal.TryParse(raw, out var value) ? value : 0;
    }

    private static string GetString(IReadOnlyDictionary<string, string?> map, string name)
    {
        if (!map.TryGetValue(name, out var raw) || string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return raw;
    }

    private static int GetRewardPoints(IReadOnlyList<RuleCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (!string.Equals(condition.EntityCode, "rule", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(condition.AttributeCode, "rewardPoints", StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryGetInt(condition.ValueJson.RootElement, out var points))
                return points;
        }

        return 0;
    }

    private static bool TryGetInt(JsonElement element, out int value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
            return true;

        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value))
            return true;

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decimalValue))
        {
            value = (int)decimalValue;
            return true;
        }

        value = 0;
        return false;
    }

    private static string? ToScalarString(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.ToString(),
            JsonValueKind.True => true.ToString(),
            JsonValueKind.False => false.ToString(),
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => el.GetRawText()
        };
    }

    private static JsonDocument BuildConditionsDocument(IReadOnlyList<RuleCondition> rootConditions)
    {
        var obj = new JsonObject();
        if (rootConditions is not null)
        {
            foreach (var condition in rootConditions.OrderBy(c => c.SortOrder))
            {
                obj[condition.AttributeCode] = JsonNode.Parse(condition.ValueJson.RootElement.GetRawText());
            }
        }

        return JsonDocument.Parse(obj.ToJsonString());
    }
}
