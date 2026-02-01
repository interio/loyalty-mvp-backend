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
            .Include(r => r.RootGroup)
            .ThenInclude(g => g.Conditions)
            .Where(r =>
                r.TenantId == tenantId &&
                r.Active &&
                r.EffectiveFrom <= now &&
                (r.EffectiveTo == null || r.EffectiveTo >= now))
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var rules = new List<IInvoicePointsRule>();
        foreach (var row in rows)
        {
            try
            {
                var parsed = ParseRule(row);
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
                        BuildConditionsDocument(row));
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

    private static IInvoicePointsRule? ParseRule(PointsRule rule)
    {
        var conditionMap = BuildConditionMap(rule);
        if (conditionMap.Count == 0)
            return null;

        var type = rule.RuleType.Trim().ToLowerInvariant();

        switch (type)
        {
            case "spend":
            case "spend_rule":
                {
                    var spendStep = GetDecimal(conditionMap, "spendStep");
                    var rewardPoints = GetInt(conditionMap, "rewardPoints");
                    if (spendStep <= 0 || rewardPoints <= 0)
                        throw new ArgumentException("Spend rule requires spendStep > 0 and rewardPoints > 0.");
                    return new SpendRule(spendStep, rewardPoints);
                }
            case "sku_quantity":
            case "sku_quantity_rule":
                {
                    var sku = GetString(conditionMap, "sku");
                    var quantityStep = GetDecimal(conditionMap, "quantityStep");
                    var rewardPoints = GetInt(conditionMap, "rewardPoints");
                    if (string.IsNullOrWhiteSpace(sku) || quantityStep <= 0 || rewardPoints <= 0)
                        throw new ArgumentException("Sku quantity rule requires sku, quantityStep > 0, rewardPoints > 0.");
                    return new SkuQuantityRule(sku, quantityStep, rewardPoints);
                }
            default:
                throw new ArgumentException($"Unsupported rule type: {rule.RuleType}");
        }
    }

    private static IReadOnlyDictionary<string, string?> BuildConditionMap(PointsRule rule)
    {
        return rule.ConditionEntries
            .GroupBy(entry => entry.Key)
            .ToDictionary(entry => entry.Key, entry => entry.First().Value);
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

    private static JsonDocument BuildConditionsDocument(PointsRule rule)
    {
        var obj = new JsonObject();
        if (rule.RootGroup?.Conditions is not null)
        {
            foreach (var condition in rule.RootGroup.Conditions.OrderBy(c => c.SortOrder))
            {
                obj[condition.AttributeCode] = JsonNode.Parse(condition.ValueJson.RootElement.GetRawText());
            }
        }

        return JsonDocument.Parse(obj.ToJsonString());
    }
}
