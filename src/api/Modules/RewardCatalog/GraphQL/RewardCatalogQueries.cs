using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.RewardCatalog.GraphQL;

/// <summary>Reward catalog read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class RewardCatalogQueries
{
    public Task<List<RewardProduct>> RewardProducts(Guid? tenantId, [Service] RewardCatalogService catalog) =>
        SafeExecute(() => catalog.ListAsync(tenantId));

    public Task<RewardProductConnection> RewardProductsPage(
        Guid? tenantId,
        int page,
        int pageSize,
        string? search,
        [Service] RewardCatalogService catalog) =>
        SafeExecute(async () =>
        {
            var result = await catalog.ListPageAsync(tenantId, page, pageSize, search);
            return new RewardProductConnection(
                result.Items,
                new PageInfo(result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        });

    public Task<List<RewardProduct>> RewardProductsSearch(string search, Guid? tenantId, [Service] RewardCatalogService catalog) =>
        SafeExecute(() => catalog.SearchAsync(search, tenantId));

    public Task<RewardProduct?> RewardProduct(Guid tenantId, Guid id, [Service] RewardCatalogDbContext db) =>
        SafeExecute(() =>
            db.RewardProducts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id));

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

public record RewardProductConnection(IReadOnlyList<RewardProduct> Nodes, PageInfo PageInfo);
