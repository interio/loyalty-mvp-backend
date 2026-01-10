using System.Text.Json;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RulesEngine.Application;

/// <summary>Manages points rules (upsert/delete) in the integration store.</summary>
public class PointsRuleService
{
    private readonly IntegrationDbContext _db;

    public PointsRuleService(IntegrationDbContext db) => _db = db;

    public Task<List<PointsRule>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.PointsRules
           .AsNoTracking()
           .Where(r => r.TenantId == tenantId)
           .OrderBy(r => r.Priority)
           .ThenBy(r => r.CreatedAt)
           .ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, Guid tenantId, CancellationToken ct = default) =>
        _db.PointsRules.AnyAsync(r => r.Id == id && r.TenantId == tenantId, ct);

    public async Task UpsertAsync(IEnumerable<PointsRuleUpsertRequest> requests, CancellationToken ct = default)
    {
        var list = requests?.ToList() ?? new();
        if (list.Count == 0) throw new ArgumentException("At least one rule is required.");

        foreach (var req in list)
        {
            Validate(req);
            var conditions = req.Conditions is null
                ? JsonDocument.Parse("{}")
                : JsonSerializer.SerializeToDocument(req.Conditions);

            PointsRule? rule = null;
            if (req.Id.HasValue)
            {
                rule = await _db.PointsRules.FirstOrDefaultAsync(r => r.Id == req.Id.Value, ct);
            }

            if (rule is null)
            {
                rule = new PointsRule
                {
                    Id = req.Id ?? Guid.NewGuid(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    RuleVersion = 1
                };
                _db.PointsRules.Add(rule);
            }
            else
            {
                if (rule.TenantId != req.TenantId)
                    throw new ArgumentException("Rule does not belong to tenant.");

                rule.UpdatedAt = DateTimeOffset.UtcNow;
                rule.RuleVersion += 1;
            }

            rule.TenantId = req.TenantId;
            rule.RuleType = req.RuleType.Trim();
            rule.Active = req.Active;
            rule.Priority = req.Priority;
            rule.EffectiveFrom = req.EffectiveFrom ?? DateTimeOffset.UtcNow;
            rule.EffectiveTo = req.EffectiveTo;
            rule.Conditions = conditions;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var rule = await _db.PointsRules.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
        if (rule is null)
            throw new System.Collections.Generic.KeyNotFoundException("Rule not found for tenant.");

        _db.PointsRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(PointsRuleUpsertRequest req)
    {
        if (req.TenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");
        if (string.IsNullOrWhiteSpace(req.RuleType)) throw new ArgumentException("ruleType is required.");
    }
}

public class PointsRuleUpsertRequest
{
    public Guid? Id { get; set; }
    public Guid TenantId { get; set; }
    public string RuleType { get; set; } = default!;
    public Dictionary<string, object?>? Conditions { get; set; }
    public bool Active { get; set; } = true;
    public int Priority { get; set; } = 0;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
}

public class PointsRuleUpsertBatchRequest
{
    public List<PointsRuleUpsertRequest> Rules { get; set; } = new();
}
