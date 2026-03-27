using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Application;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.RulesEngine.GraphQL;

/// <summary>Points rules read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class PointsRuleQueries
{
    /// <summary>Lists points rules for a tenant.</summary>
    public Task<List<PointsRule>> PointsRulesByTenant(Guid tenantId, [Service] PointsRuleService rules) =>
        SafeExecute(() => rules.ListByTenantAsync(tenantId));

    /// <summary>Pages points rules for a tenant.</summary>
    public Task<PointsRuleConnection> PointsRulesByTenantPage(
        Guid tenantId,
        int page,
        int pageSize,
        [Service] PointsRuleService rules) =>
        SafeExecute(async () =>
        {
            var result = await rules.ListByTenantPageAsync(tenantId, page, pageSize);
            return new PointsRuleConnection(
                result.Items,
                new PageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        });

    /// <summary>Lists rule campaigns for dashboard usage.</summary>
    public Task<List<CampaignRuleSummary>> CampaignRulesByTenant(
        Guid tenantId,
        [Service] PointsRuleService rules) =>
        SafeExecute(() => rules.ListCampaignRulesByTenantAsync(tenantId));

    /// <summary>Gets the nested condition tree for a rule.</summary>
    public Task<RuleConditionTreeGroup> RuleConditionTree(
        Guid ruleId,
        Guid tenantId,
        [Service] PointsRuleService rules) =>
        SafeExecute(() => rules.GetConditionTreeAsync(ruleId, tenantId));

    /// <summary>Gets a flat condition tree payload for a rule.</summary>
    public Task<RuleConditionTreeFlat> RuleConditionTreeFlat(
        Guid ruleId,
        Guid tenantId,
        [Service] PointsRuleService rules) =>
        SafeExecute(() => rules.GetConditionTreeFlatAsync(ruleId, tenantId));

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

public record PointsRuleConnection(IReadOnlyList<PointsRule> Nodes, PageInfo PageInfo);
