using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RewardCatalog.GraphQL;

/// <summary>Reward catalog read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class RewardCatalogQueries
{
    public Task<List<RewardProduct>> RewardProducts(Guid? tenantId, [Service] RewardCatalogService catalog) =>
        SafeExecute(() => catalog.ListAsync(tenantId));

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
