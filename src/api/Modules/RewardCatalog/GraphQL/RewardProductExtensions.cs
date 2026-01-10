using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RewardCatalog.GraphQL;

/// <summary>Extra fields for reward products.</summary>
[ExtendObjectType(typeof(RewardProduct))]
public class RewardProductExtensions
{
    public async Task<int> InventoryQuantity([Parent] RewardProduct product, [Service] RewardCatalogDbContext db, CancellationToken ct)
    {
        var inventory = await db.RewardInventories
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.RewardProductId == product.Id, ct);

        return inventory?.AvailableQuantity ?? 0;
    }
}
