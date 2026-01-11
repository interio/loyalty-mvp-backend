using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.Tenants.Application;
using Loyalty.Api.Modules.Tenants.Domain;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.Tenants.GraphQL;

/// <summary>Tenant read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class TenantQueries
{
    /// <summary>Lists tenants (admin convenience).</summary>
    public Task<List<Tenant>> Tenants([Service] ITenantService tenants) =>
        SafeExecute(() => tenants.ListAsync());

    public Task<TenantConnection> TenantsPage(
        int page,
        int pageSize,
        [Service] ITenantService tenants) =>
        SafeExecute(async () =>
        {
            var result = await tenants.ListPageAsync(page, pageSize);
            return new TenantConnection(
                result.Items,
                new PageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
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

public record TenantConnection(IReadOnlyList<Tenant> Nodes, PageInfo PageInfo);
