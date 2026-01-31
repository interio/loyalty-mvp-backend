using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RulesEngine.GraphQL;

/// <summary>Rule entity CRUD mutations for the admin UI.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class RuleEntityMutations
{
    public Task<RuleEntity> CreateRuleEntity(CreateRuleEntityInput input, [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var code = NormalizeCode(input.Code);
            var displayName = NormalizeDisplayName(input.DisplayName);
            var tenantId = NormalizeTenantId(input.TenantId);

            var exists = await db.RuleEntities.AnyAsync(e => e.TenantId == tenantId && e.Code == code);
            if (exists)
            {
                throw new ArgumentException("Entity code already exists for this tenant scope.");
            }

            var entity = new RuleEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                DisplayName = displayName,
                IsActive = input.IsActive ?? true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.RuleEntities.Add(entity);
            await db.SaveChangesAsync();
            return entity;
        });

    public Task<RuleEntity> UpdateRuleEntity(UpdateRuleEntityInput input, [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var entity = await db.RuleEntities.FirstOrDefaultAsync(e => e.Id == input.Id);
            if (entity is null)
                throw new ArgumentException("Rule entity not found.");

            var code = NormalizeCode(input.Code);
            var displayName = NormalizeDisplayName(input.DisplayName);
            var tenantId = NormalizeTenantId(input.TenantId);

            var exists = await db.RuleEntities
                .AnyAsync(e => e.Id != entity.Id && e.TenantId == tenantId && e.Code == code);
            if (exists)
                throw new ArgumentException("Entity code already exists for this tenant scope.");

            entity.TenantId = tenantId;
            entity.Code = code;
            entity.DisplayName = displayName;
            entity.IsActive = input.IsActive;

            await db.SaveChangesAsync();
            return entity;
        });

    public Task<bool> DeleteRuleEntity(Guid id, [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var entity = await db.RuleEntities.FirstOrDefaultAsync(e => e.Id == id);
            if (entity is null)
                throw new ArgumentException("Rule entity not found.");

            db.RuleEntities.Remove(entity);
            await db.SaveChangesAsync();
            return true;
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

    private static Guid? NormalizeTenantId(Guid? tenantId) =>
        tenantId == Guid.Empty ? null : tenantId;

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

public record CreateRuleEntityInput(string Code, string DisplayName, bool? IsActive, Guid? TenantId);

public record UpdateRuleEntityInput(Guid Id, string Code, string DisplayName, bool IsActive, Guid? TenantId);
