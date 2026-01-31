using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RulesEngine.GraphQL;

/// <summary>Rule catalog metadata queries for the admin rule builder.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class RuleCatalogQueries
{
    public Task<List<RuleEntity>> RuleEntities([Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
            await db.RuleEntities
                .AsNoTracking()
                .Where(e => e.TenantId == null && e.IsActive)
                .OrderBy(e => e.DisplayName)
                .ToListAsync());

    public Task<List<RuleAttribute>> RuleAttributes(
        string entityCode,
        [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var code = entityCode.Trim();
            if (string.IsNullOrWhiteSpace(code)) return new List<RuleAttribute>();

            var entity = await db.RuleEntities
                .AsNoTracking()
                .Where(e => e.TenantId == null && e.IsActive && e.Code == code)
                .Select(e => new { e.Id })
                .FirstOrDefaultAsync();

            if (entity is null) return new List<RuleAttribute>();

            return await db.RuleAttributes
                .AsNoTracking()
                .Where(a => a.EntityId == entity.Id && a.IsQueryable)
                .OrderBy(a => a.DisplayName)
                .ToListAsync();
        });

    public Task<List<RuleAttributeOperator>> RuleAttributeOperators(
        Guid attributeId,
        [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
            await db.RuleAttributeOperators
                .AsNoTracking()
                .Where(o => o.AttributeId == attributeId)
                .OrderBy(o => o.Operator)
                .ToListAsync());

    public Task<List<RuleAttributeOption>> RuleAttributeOptions(
        Guid attributeId,
        [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
            await db.RuleAttributeOptions
                .AsNoTracking()
                .Where(o => o.AttributeId == attributeId)
                .OrderBy(o => o.Label)
                .ToListAsync());

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
