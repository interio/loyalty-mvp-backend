using Loyalty.Api.Modules.RewardCatalog.Domain;

namespace Loyalty.Api.Modules.RewardCatalog.Application;

/// <summary>Read-only access to reward catalog for cross-module use.</summary>
public interface IRewardCatalogLookup
{
    Task<List<RewardProduct>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}

/// <summary>Inventory operations for reward catalog.</summary>
public interface IRewardInventoryService
{
    Task ReserveAsync(Guid rewardProductId, int quantity, CancellationToken ct = default);
    Task ReleaseAsync(Guid rewardProductId, int quantity, CancellationToken ct = default);
}
