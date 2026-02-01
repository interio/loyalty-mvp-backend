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
           .ThenInclude(g => g!.Conditions)
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
           .ThenInclude(g => g!.Conditions)
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

    public async Task<Guid> CreateComplexRuleAsync(ComplexRuleCreateRequest request, CancellationToken ct = default)
    {
        ValidateComplex(request);

        using var tx = await _db.Database.BeginTransactionAsync(ct);

        var rule = new PointsRule
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Name = request.Name.Trim(),
            RuleType = string.IsNullOrWhiteSpace(request.RuleType) ? "complex_rule" : request.RuleType.Trim(),
            Active = request.Active,
            Priority = request.Priority,
            EffectiveFrom = request.EffectiveFrom ?? DateTimeOffset.UtcNow,
            EffectiveTo = request.EffectiveTo,
            CreatedAt = DateTimeOffset.UtcNow,
            RuleVersion = 1
        };

        _db.PointsRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        var rootGroup = new RuleConditionGroup
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            ParentGroupId = null,
            Logic = request.RootGroup.Logic?.Trim().ToUpperInvariant() ?? "AND",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.RuleConditionGroups.Add(rootGroup);
        await _db.SaveChangesAsync(ct);

        rule.RootGroupId = rootGroup.Id;
        await _db.SaveChangesAsync(ct);

        var rootSortOrder = 0;
        if (request.PointsToGrant > 0)
        {
            _db.RuleConditions.Add(new RuleCondition
            {
                Id = Guid.NewGuid(),
                GroupId = rootGroup.Id,
                EntityCode = "rule",
                AttributeCode = "rewardPoints",
                Operator = "eq",
                ValueJson = JsonSerializer.SerializeToDocument(request.PointsToGrant),
                SortOrder = rootSortOrder++,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        AddConditionNodes(rule.Id, rootGroup.Id, request.RootGroup.Children, ref rootSortOrder);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return rule.Id;
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

    public async Task<RuleConditionTreeGroup> GetConditionTreeAsync(Guid ruleId, Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var rule = await _db.PointsRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.TenantId == tenantId, ct);

        if (rule is null)
            throw new System.Collections.Generic.KeyNotFoundException("Rule not found for tenant.");

        if (!rule.RootGroupId.HasValue)
            throw new ArgumentException("Rule has no root condition group.");

        var groups = await _db.RuleConditionGroups
            .AsNoTracking()
            .Where(g => g.RuleId == ruleId)
            .ToListAsync(ct);

        if (groups.Count == 0)
            throw new ArgumentException("Rule has no condition groups.");

        var groupMap = groups.ToDictionary(g => g.Id, g => g);
        var groupIds = groupMap.Keys.ToList();

        var conditions = await _db.RuleConditions
            .AsNoTracking()
            .Where(c => groupIds.Contains(c.GroupId))
            .ToListAsync(ct);

        var groupsByParent = groups
            .Where(g => g.ParentGroupId.HasValue)
            .GroupBy(g => g.ParentGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var conditionsByGroup = conditions
            .GroupBy(c => c.GroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        RuleConditionTreeGroup BuildGroup(Guid groupId)
        {
            if (!groupMap.TryGetValue(groupId, out var group))
                throw new ArgumentException("Condition group not found.");

            var nodes = new List<RuleConditionTreeNode>();

            if (groupsByParent.TryGetValue(groupId, out var childGroups))
            {
                foreach (var childGroup in childGroups)
                {
                    nodes.Add(new RuleConditionTreeNode
                    {
                        Type = "group",
                        SortOrder = childGroup.SortOrder,
                        Group = BuildGroup(childGroup.Id)
                    });
                }
            }

            if (conditionsByGroup.TryGetValue(groupId, out var groupConditions))
            {
                foreach (var condition in groupConditions)
                {
                    nodes.Add(new RuleConditionTreeNode
                    {
                        Type = "condition",
                        SortOrder = condition.SortOrder,
                        Condition = new RuleConditionTreeCondition
                        {
                            Id = condition.Id,
                            EntityCode = condition.EntityCode,
                            AttributeCode = condition.AttributeCode,
                            Operator = condition.Operator,
                            ValueJson = condition.ValueJson.RootElement.GetRawText(),
                            SortOrder = condition.SortOrder
                        }
                    });
                }
            }

            var ordered = nodes.OrderBy(n => n.SortOrder).ToList();

            return new RuleConditionTreeGroup
            {
                Id = group.Id,
                Logic = group.Logic,
                Children = ordered
            };
        }

        return BuildGroup(rule.RootGroupId.Value);
    }

    public async Task<RuleConditionTreeFlat> GetConditionTreeFlatAsync(Guid ruleId, Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var rule = await _db.PointsRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.TenantId == tenantId, ct);

        if (rule is null)
            throw new System.Collections.Generic.KeyNotFoundException("Rule not found for tenant.");

        if (!rule.RootGroupId.HasValue)
            throw new ArgumentException("Rule has no root condition group.");

        var groups = await _db.RuleConditionGroups
            .AsNoTracking()
            .Where(g => g.RuleId == ruleId)
            .ToListAsync(ct);

        if (groups.Count == 0)
            throw new ArgumentException("Rule has no condition groups.");

        var groupIds = groups.Select(g => g.Id).ToList();

        var conditions = await _db.RuleConditions
            .AsNoTracking()
            .Where(c => groupIds.Contains(c.GroupId))
            .ToListAsync(ct);

        return new RuleConditionTreeFlat
        {
            RootGroupId = rule.RootGroupId.Value,
            Groups = groups
                .OrderBy(g => g.SortOrder)
                .Select(g => new RuleConditionTreeGroupFlat
                {
                    Id = g.Id,
                    ParentGroupId = g.ParentGroupId,
                    Logic = g.Logic,
                    SortOrder = g.SortOrder
                })
                .ToList(),
            Conditions = conditions
                .OrderBy(c => c.SortOrder)
                .Select(c => new RuleConditionTreeConditionFlat
                {
                    Id = c.Id,
                    GroupId = c.GroupId,
                    EntityCode = c.EntityCode,
                    AttributeCode = c.AttributeCode,
                    Operator = c.Operator,
                    ValueJson = c.ValueJson.RootElement.GetRawText(),
                    SortOrder = c.SortOrder
                })
                .ToList()
        };
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

    private static void ValidateComplex(ComplexRuleCreateRequest req)
    {
        if (req.TenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("name is required.");
        if (req.PointsToGrant <= 0) throw new ArgumentException("pointsToGrant must be greater than 0.");
        if (req.RootGroup is null) throw new ArgumentException("rootGroup is required.");
        if (req.RootGroup.Children is null || req.RootGroup.Children.Count == 0)
            throw new ArgumentException("At least one condition is required.");

        foreach (var child in req.RootGroup.Children)
        {
            ValidateNode(child);
        }
    }

    private static void ValidateNode(RuleConditionNodeInput node)
    {
        if (node is null) throw new ArgumentException("Condition node is required.");
        var type = node.Type?.Trim().ToLowerInvariant();
        if (type == "group")
        {
            if (string.IsNullOrWhiteSpace(node.Logic))
                throw new ArgumentException("Group logic is required.");
            if (node.Children is null || node.Children.Count == 0)
                throw new ArgumentException("Group must contain at least one condition.");
            foreach (var child in node.Children)
            {
                ValidateNode(child);
            }
            return;
        }

        if (type == "condition")
        {
            if (string.IsNullOrWhiteSpace(node.EntityCode))
                throw new ArgumentException("EntityCode is required for a condition.");
            if (string.IsNullOrWhiteSpace(node.AttributeCode))
                throw new ArgumentException("AttributeCode is required for a condition.");
            if (string.IsNullOrWhiteSpace(node.Operator))
                throw new ArgumentException("Operator is required for a condition.");
            if (node.ValueJson is null)
                throw new ArgumentException("ValueJson is required for a condition.");
            return;
        }

        throw new ArgumentException("Unknown node type.");
    }

    private void AddConditionNodes(
        Guid ruleId,
        Guid parentGroupId,
        IReadOnlyList<RuleConditionNodeInput> nodes,
        ref int sortOrder)
    {
        if (nodes is null) return;

        foreach (var node in nodes)
        {
            var type = node.Type?.Trim().ToLowerInvariant();
            if (type == "group")
            {
                var group = new RuleConditionGroup
                {
                    Id = Guid.NewGuid(),
                    RuleId = ruleId,
                    ParentGroupId = parentGroupId,
                    Logic = node.Logic?.Trim().ToUpperInvariant() ?? "AND",
                    SortOrder = sortOrder++,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _db.RuleConditionGroups.Add(group);
                var nestedOrder = 0;
                AddConditionNodes(ruleId, group.Id, node.Children ?? new List<RuleConditionNodeInput>(), ref nestedOrder);
            }
            else
            {
                _db.RuleConditions.Add(new RuleCondition
                {
                    Id = Guid.NewGuid(),
                    GroupId = parentGroupId,
                    EntityCode = node.EntityCode!.Trim(),
                    AttributeCode = node.AttributeCode!.Trim(),
                    Operator = node.Operator!.Trim(),
                    ValueJson = JsonDocument.Parse(node.ValueJson!.Value.GetRawText()),
                    SortOrder = sortOrder++,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }
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

public class ComplexRuleCreateRequest
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? RuleType { get; set; }
    public bool Active { get; set; } = true;
    public int Priority { get; set; } = 0;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public int PointsToGrant { get; set; }
    public RuleConditionGroupInput RootGroup { get; set; } = default!;
}

public class RuleConditionGroupInput
{
    public string? Logic { get; set; }
    public List<RuleConditionNodeInput> Children { get; set; } = new();
}

public class RuleConditionNodeInput
{
    public string? Type { get; set; }
    public string? Logic { get; set; }
    public List<RuleConditionNodeInput>? Children { get; set; }
    public string? EntityCode { get; set; }
    public string? AttributeCode { get; set; }
    public string? Operator { get; set; }
    public JsonElement? ValueJson { get; set; }
}

public class RuleConditionTreeGroup
{
    public Guid Id { get; set; }
    public string Logic { get; set; } = "AND";
    public IReadOnlyList<RuleConditionTreeNode> Children { get; set; } = Array.Empty<RuleConditionTreeNode>();
}

public class RuleConditionTreeNode
{
    public string Type { get; set; } = "condition";
    public int SortOrder { get; set; }
    public RuleConditionTreeGroup? Group { get; set; }
    public RuleConditionTreeCondition? Condition { get; set; }
}

public class RuleConditionTreeCondition
{
    public Guid Id { get; set; }
    public string EntityCode { get; set; } = default!;
    public string AttributeCode { get; set; } = default!;
    public string Operator { get; set; } = default!;
    public string ValueJson { get; set; } = default!;
    public int SortOrder { get; set; }
}

public class RuleConditionTreeFlat
{
    public Guid RootGroupId { get; set; }
    public IReadOnlyList<RuleConditionTreeGroupFlat> Groups { get; set; } = Array.Empty<RuleConditionTreeGroupFlat>();
    public IReadOnlyList<RuleConditionTreeConditionFlat> Conditions { get; set; } = Array.Empty<RuleConditionTreeConditionFlat>();
}

public class RuleConditionTreeGroupFlat
{
    public Guid Id { get; set; }
    public Guid? ParentGroupId { get; set; }
    public string Logic { get; set; } = "AND";
    public int SortOrder { get; set; }
}

public class RuleConditionTreeConditionFlat
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string EntityCode { get; set; } = default!;
    public string AttributeCode { get; set; } = default!;
    public string Operator { get; set; } = default!;
    public string ValueJson { get; set; } = default!;
    public int SortOrder { get; set; }
}
