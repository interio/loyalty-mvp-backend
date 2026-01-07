using System.Text.Json;
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

        var rules = new List<IInvoicePointsRule>();
        foreach (var row in rows)
        {
            try
            {
                var parsed = ParseRule(row);
                if (parsed != null)
                {
                    var conditionsJson = row.Conditions?.RootElement.GetRawText() ?? "{}";
                    var metadata = new InvoiceRuleMetadata(
                        row.Id,
                        row.RuleVersion,
                        row.RuleType,
                        row.Priority,
                        row.Active,
                        row.EffectiveFrom,
                        row.EffectiveTo,
                        JsonDocument.Parse(conditionsJson));
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
        if (rule.Conditions is null)
            return null;

        var type = rule.RuleType.Trim().ToLowerInvariant();
        var root = rule.Conditions.RootElement;

        switch (type)
        {
            case "spend":
            case "spend_rule":
                {
                    var spendStep = GetDecimal(root, "spendStep");
                    var rewardPoints = GetInt(root, "rewardPoints");
                    if (spendStep <= 0 || rewardPoints <= 0)
                        throw new ArgumentException("Spend rule requires spendStep > 0 and rewardPoints > 0.");
                    return new SpendRule(spendStep, rewardPoints);
                }
            case "sku_quantity":
            case "sku_quantity_rule":
                {
                    var sku = GetString(root, "sku");
                    var quantityStep = GetDecimal(root, "quantityStep");
                    var rewardPoints = GetInt(root, "rewardPoints");
                    if (string.IsNullOrWhiteSpace(sku) || quantityStep <= 0 || rewardPoints <= 0)
                        throw new ArgumentException("Sku quantity rule requires sku, quantityStep > 0, rewardPoints > 0.");
                    return new SkuQuantityRule(sku, quantityStep, rewardPoints);
                }
            default:
                throw new ArgumentException($"Unsupported rule type: {rule.RuleType}");
        }
    }

    private static int GetInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop)) return 0;
        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt32(out var v) => v,
            JsonValueKind.String when int.TryParse(prop.GetString(), out var v) => v,
            _ => 0
        };
    }

    private static decimal GetDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop)) return 0;
        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetDecimal(out var v) => v,
            JsonValueKind.String when decimal.TryParse(prop.GetString(), out var v) => v,
            _ => 0
        };
    }

    private static string GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop)) return string.Empty;
        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? string.Empty,
            _ => prop.ToString()
        };
    }
}
