using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RulesEngine.GraphQL;

/// <summary>Rule attribute CRUD mutations for the admin UI.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class RuleAttributeMutations
{
    public Task<RuleAttribute> CreateRuleAttribute(CreateRuleAttributeInput input, [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var entity = await db.RuleEntities.FirstOrDefaultAsync(e => e.Id == input.EntityId);
            if (entity is null)
                throw new ArgumentException("Rule entity not found.");

            var code = NormalizeCode(input.Code);
            var displayName = NormalizeDisplayName(input.DisplayName);
            var valueType = NormalizeEnum(input.ValueType, "valueType");
            var uiControl = NormalizeEnum(input.UiControl, "uiControl");

            var exists = await db.RuleAttributes.AnyAsync(a => a.EntityId == entity.Id && a.Code == code);
            if (exists)
                throw new ArgumentException("Attribute code already exists for this entity.");

            var attribute = new RuleAttribute
            {
                Id = Guid.NewGuid(),
                TenantId = entity.TenantId,
                EntityId = entity.Id,
                Code = code,
                DisplayName = displayName,
                ValueType = valueType,
                IsMultiValue = input.IsMultiValue,
                IsQueryable = input.IsQueryable,
                UiControl = uiControl,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.RuleAttributes.Add(attribute);
            await db.SaveChangesAsync();
            return attribute;
        });

    public Task<RuleAttribute> UpdateRuleAttribute(UpdateRuleAttributeInput input, [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var attribute = await db.RuleAttributes.FirstOrDefaultAsync(a => a.Id == input.Id);
            if (attribute is null)
                throw new ArgumentException("Rule attribute not found.");

            var code = NormalizeCode(input.Code);
            var displayName = NormalizeDisplayName(input.DisplayName);
            var valueType = NormalizeEnum(input.ValueType, "valueType");
            var uiControl = NormalizeEnum(input.UiControl, "uiControl");

            var exists = await db.RuleAttributes
                .AnyAsync(a => a.Id != attribute.Id && a.EntityId == attribute.EntityId && a.Code == code);
            if (exists)
                throw new ArgumentException("Attribute code already exists for this entity.");

            attribute.Code = code;
            attribute.DisplayName = displayName;
            attribute.ValueType = valueType;
            attribute.IsMultiValue = input.IsMultiValue;
            attribute.IsQueryable = input.IsQueryable;
            attribute.UiControl = uiControl;

            await db.SaveChangesAsync();
            return attribute;
        });

    public Task<bool> DeleteRuleAttribute(Guid id, [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var attribute = await db.RuleAttributes.FirstOrDefaultAsync(a => a.Id == id);
            if (attribute is null)
                throw new ArgumentException("Rule attribute not found.");

            db.RuleAttributes.Remove(attribute);
            await db.SaveChangesAsync();
            return true;
        });

    public Task<List<RuleAttributeOperator>> SetRuleAttributeOperators(
        SetRuleAttributeOperatorsInput input,
        [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var attribute = await db.RuleAttributes.FirstOrDefaultAsync(a => a.Id == input.AttributeId);
            if (attribute is null)
                throw new ArgumentException("Rule attribute not found.");

            var normalized = input.Operators
                .Select(o => NormalizeEnum(o, "operator"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var op in normalized)
            {
                if (!RuleOperatorCatalog.AllowedValues.Contains(op))
                    throw new ArgumentException($"Unsupported operator '{op}'.");
            }

            var existing = await db.RuleAttributeOperators
                .Where(o => o.AttributeId == attribute.Id)
                .ToListAsync();

            if (existing.Count > 0)
            {
                db.RuleAttributeOperators.RemoveRange(existing);
            }

            var created = new List<RuleAttributeOperator>();
            foreach (var op in normalized)
            {
                var ruleOp = new RuleAttributeOperator
                {
                    Id = Guid.NewGuid(),
                    TenantId = attribute.TenantId,
                    AttributeId = attribute.Id,
                    Operator = op
                };
                created.Add(ruleOp);
                db.RuleAttributeOperators.Add(ruleOp);
            }

            await db.SaveChangesAsync();
            return created;
        });

    private static string NormalizeCode(string? code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Code is required.");
        return trimmed.ToLowerInvariant();
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Display name is required.");
        return trimmed;
    }

    private static string NormalizeEnum(string? value, string fieldName)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException($"{fieldName} is required.");
        return trimmed.ToLowerInvariant();
    }

    private static async Task<T> SafeExecute<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }
}

public record CreateRuleAttributeInput(
    Guid EntityId,
    string Code,
    string DisplayName,
    string ValueType,
    bool IsMultiValue,
    bool IsQueryable,
    string UiControl);

public record UpdateRuleAttributeInput(
    Guid Id,
    string Code,
    string DisplayName,
    string ValueType,
    bool IsMultiValue,
    bool IsQueryable,
    string UiControl);

public record SetRuleAttributeOperatorsInput(Guid AttributeId, List<string> Operators);
