using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Domain;

namespace Loyalty.Api.Modules.RulesEngine.GraphQL;

/// <summary>Points rules read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class PointsRuleQueries
{
    /// <summary>Lists points rules for a tenant.</summary>
    public Task<List<PointsRule>> PointsRulesByTenant(Guid tenantId, [Service] PointsRuleService rules) =>
        SafeExecute(() => rules.ListByTenantAsync(tenantId));

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
