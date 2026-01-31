using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RulesEngine.GraphQL;

/// <summary>Rule attribute option CRUD mutations for the admin UI.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class RuleAttributeOptionMutations
{
    public Task<RuleAttributeOption> CreateRuleAttributeOption(
        CreateRuleAttributeOptionInput input,
        [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var attribute = await db.RuleAttributes.FirstOrDefaultAsync(a => a.Id == input.AttributeId);
            if (attribute is null)
                throw new ArgumentException("Rule attribute not found.");

            var value = NormalizeValue(input.Value);
            var label = NormalizeLabel(input.Label);

            var exists = await db.RuleAttributeOptions
                .AnyAsync(o => o.AttributeId == attribute.Id && o.Value == value);
            if (exists)
                throw new ArgumentException("Option value already exists for this attribute.");

            var option = new RuleAttributeOption
            {
                Id = Guid.NewGuid(),
                TenantId = attribute.TenantId,
                AttributeId = attribute.Id,
                Value = value,
                Label = label
            };

            db.RuleAttributeOptions.Add(option);
            await db.SaveChangesAsync();
            return option;
        });

    public Task<RuleAttributeOption> UpdateRuleAttributeOption(
        UpdateRuleAttributeOptionInput input,
        [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var option = await db.RuleAttributeOptions.FirstOrDefaultAsync(o => o.Id == input.Id);
            if (option is null)
                throw new ArgumentException("Rule attribute option not found.");

            var value = NormalizeValue(input.Value);
            var label = NormalizeLabel(input.Label);

            var exists = await db.RuleAttributeOptions
                .AnyAsync(o => o.Id != option.Id && o.AttributeId == option.AttributeId && o.Value == value);
            if (exists)
                throw new ArgumentException("Option value already exists for this attribute.");

            option.Value = value;
            option.Label = label;

            await db.SaveChangesAsync();
            return option;
        });

    public Task<bool> DeleteRuleAttributeOption(Guid id, [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var option = await db.RuleAttributeOptions.FirstOrDefaultAsync(o => o.Id == id);
            if (option is null)
                throw new ArgumentException("Rule attribute option not found.");

            db.RuleAttributeOptions.Remove(option);
            await db.SaveChangesAsync();
            return true;
        });

    private static string NormalizeValue(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Value is required.");
        return trimmed.ToLowerInvariant();
    }

    private static string NormalizeLabel(string? label)
    {
        var trimmed = label?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Label is required.");
        return trimmed;
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

public record CreateRuleAttributeOptionInput(Guid AttributeId, string Value, string Label);

public record UpdateRuleAttributeOptionInput(Guid Id, string Value, string Label);
