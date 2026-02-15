using System.Security.Claims;
using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Products.Application;
using Loyalty.Api.Modules.Products.Domain;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.Products.GraphQL;

/// <summary>Distributor read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class DistributorQueries
{
    public Task<List<Distributor>> DistributorsByTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        [Service] DistributorService distributors) =>
        SafeExecute(() =>
        {
            var scopedTenantId = ProductTenantScopeResolver.Resolve(tenantId, user);
            return distributors.ListByTenantAsync(scopedTenantId);
        });

    public Task<DistributorConnection> DistributorsByTenantPage(
        Guid tenantId,
        int page,
        int pageSize,
        string? search,
        ClaimsPrincipal user,
        [Service] DistributorService distributors) =>
        SafeExecute(async () =>
        {
            var scopedTenantId = ProductTenantScopeResolver.Resolve(tenantId, user);
            var result = await distributors.ListByTenantPageAsync(scopedTenantId, page, pageSize, search);
            return new DistributorConnection(
                result.Items,
                new PageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        });

    public Task<List<Distributor>> DistributorsByTenantSearch(
        Guid tenantId,
        string search,
        ClaimsPrincipal user,
        [Service] DistributorService distributors) =>
        SafeExecute(() =>
        {
            var scopedTenantId = ProductTenantScopeResolver.Resolve(tenantId, user);
            return distributors.SearchByTenantAsync(scopedTenantId, search);
        });

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

public record DistributorConnection(IReadOnlyList<Distributor> Nodes, PageInfo PageInfo);
