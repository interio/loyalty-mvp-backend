using System.Text.Json;
using System.Linq;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.RulesEngine.Application;

/// <summary>Manages points rules (upsert/delete) in the integration store.</summary>
public class PointsRuleService
{
    private readonly IntegrationDbContext _db;

    public PointsRuleService(IntegrationDbContext db) => _db = db;

    public Task<List<PointsRule>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.PointsRules
           .AsNoTracking()
           .Include(r => r.RootGroup)
           .ThenInclude(g => g.Conditions)
           .Where(r => r.TenantId == tenantId)
           .OrderBy(r => r.Priority)
           .ThenBy(r => r.CreatedAt)
           .ToListAsync(ct);

    public async Task<PageResult<PointsRule>> ListByTenantPageAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var query = _db.PointsRules
           .AsNoTracking()
           .Include(r => r.RootGroup)
           .ThenInclude(g => g.Conditions)
           .Where(r => r.TenantId == tenantId)
           .OrderBy(r => r.Priority)
           .ThenBy(r => r.CreatedAt)
           .AsQueryable();

        return await query.ToPageResultAsync(page, pageSize, ct);
    }

    public Task<bool> ExistsAsync(Guid id, Guid tenantId, CancellationToken ct = default) =>
        _db.PointsRules.AnyAsync(r => r.Id == id && r.TenantId == tenantId, ct);

    public async Task UpsertAsync(IEnumerable<PointsRuleUpsertRequest> requests, CancellationToken ct = default)
    {
        var list = requests?.ToList() ?? new();
        if (list.Count == 0) throw new ArgumentException("At least one rule is required.");

        using var tx = await _db.Database.BeginTransactionAsync(ct);
        var staged = new List<(PointsRule Rule, RuleConditionGroup RootGroup, Dictionary<string, object?>? Conditions)>();

        foreach (var req in list)
        {
            Validate(req);

            if (req.Id.HasValue)
            {
                var existing = await _db.PointsRules.FirstOrDefaultAsync(r => r.Id == req.Id.Value, ct);
                if (existing is not null)
                    throw new ArgumentException("Editing existing rules is not allowed. Create a new rule instead.");
            }

            var rule = new PointsRule
            {
                Id = req.Id ?? Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                RuleVersion = 1
            };
            _db.PointsRules.Add(rule);

            rule.TenantId = req.TenantId;
            rule.Name = req.Name.Trim();
            rule.RuleType = req.RuleType.Trim();
            rule.Active = req.Active;
            rule.Priority = req.Priority;
            rule.EffectiveFrom = req.EffectiveFrom ?? DateTimeOffset.UtcNow;
            rule.EffectiveTo = req.EffectiveTo;

            var rootGroup = new RuleConditionGroup
            {
                Id = Guid.NewGuid(),
                RuleId = rule.Id,
                Logic = "AND",
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow
            };

            staged.Add((rule, rootGroup, req.Conditions));
        }

        await _db.SaveChangesAsync(ct);

        foreach (var (rule, rootGroup, conditions) in staged)
        {
            _db.RuleConditionGroups.Add(rootGroup);

            if (conditions is not null && conditions.Count > 0)
            {
                var sortOrder = 0;
                foreach (var (key, value) in conditions.OrderBy(k => k.Key))
                {
                    var valueDoc = JsonSerializer.SerializeToDocument(value);
                    _db.RuleConditions.Add(new RuleCondition
                    {
                        Id = Guid.NewGuid(),
                        GroupId = rootGroup.Id,
                        EntityCode = "rule",
                        AttributeCode = key,
                        Operator = "eq",
                        ValueJson = valueDoc,
                        SortOrder = sortOrder++,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var (rule, rootGroup, _) in staged)
        {
            rule.RootGroupId = rootGroup.Id;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task SetActiveAsync(Guid id, Guid tenantId, bool active, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var rule = await _db.PointsRules.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
        if (rule is null)
            throw new System.Collections.Generic.KeyNotFoundException("Rule not found for tenant.");

        if (rule.Active == active) return;

        rule.Active = active;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        rule.RuleVersion += 1;
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
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("name is required.");
        if (string.IsNullOrWhiteSpace(req.RuleType)) throw new ArgumentException("ruleType is required.");
    }
}


public class PointsRuleUpsertRequest
{
    public Guid? Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
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
