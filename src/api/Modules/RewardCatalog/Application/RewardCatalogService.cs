using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RewardCatalog.Application;

/// <summary>Reward catalog ingestion and lookup service.</summary>
public class RewardCatalogService : IRewardCatalogLookup, IRewardInventoryService
{
    private readonly RewardCatalogDbContext _db;

    public RewardCatalogService(RewardCatalogDbContext db) => _db = db;

    public Task<List<RewardProduct>> ListAsync(Guid? tenantId = null, int take = 500, CancellationToken ct = default)
    {
        var query = _db.RewardProducts.AsNoTracking();
        if (tenantId.HasValue)
            query = query.Where(p => p.TenantId == tenantId.Value);

        return query
            .OrderBy(p => p.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<List<RewardProduct>> SearchAsync(string search, Guid? tenantId = null, int take = 200, CancellationToken ct = default)
    {
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term)) return Task.FromResult(new List<RewardProduct>());

        var pattern = $"%{term}%";

        var query = _db.RewardProducts
           .AsNoTracking()
           .Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Sku, pattern) ||
                (p.Gtin != null && EF.Functions.ILike(p.Gtin, pattern)) ||
                EF.Functions.ILike(p.RewardVendor, pattern));

        if (tenantId.HasValue)
            query = query.Where(p => p.TenantId == tenantId.Value);

        return query
            .OrderBy(p => p.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(IEnumerable<RewardProductUpsertRequest> requests, CancellationToken ct = default)
    {
        foreach (var req in requests)
        {
            await UpsertSingleAsync(req, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<List<RewardProduct>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
        _db.RewardProducts
           .Where(p => ids.Contains(p.Id))
           .ToListAsync(ct);

    public async Task ReserveAsync(Guid rewardProductId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than 0.");

        var rows = await _db.RewardInventories
            .Where(i => i.RewardProductId == rewardProductId && i.AvailableQuantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.AvailableQuantity, i => i.AvailableQuantity - quantity)
                .SetProperty(i => i.UpdatedAt, _ => DateTimeOffset.UtcNow), ct);

        if (rows == 0)
            throw new InvalidOperationException("Insufficient inventory.");
    }

    public async Task ReleaseAsync(Guid rewardProductId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) return;

        await _db.RewardInventories
            .Where(i => i.RewardProductId == rewardProductId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.AvailableQuantity, i => i.AvailableQuantity + quantity)
                .SetProperty(i => i.UpdatedAt, _ => DateTimeOffset.UtcNow), ct);
    }

    private async Task UpsertSingleAsync(RewardProductUpsertRequest req, CancellationToken ct)
    {
        Validate(req);

        var trimmedVendor = req.RewardVendor.Trim();
        var trimmedSku = req.Sku.Trim();
        var trimmedGtin = string.IsNullOrWhiteSpace(req.Gtin) ? null : req.Gtin.Trim();

        var query = _db.RewardProducts
            .Where(p => p.TenantId == req.TenantId && p.RewardVendor == trimmedVendor && p.Sku == trimmedSku);

        if (!string.IsNullOrWhiteSpace(trimmedGtin))
            query = query.Where(p => p.Gtin == trimmedGtin);

        var product = await query.FirstOrDefaultAsync(ct);

        if (product is null)
        {
            product = new RewardProduct
            {
                TenantId = req.TenantId,
                RewardVendor = trimmedVendor,
                Sku = trimmedSku,
                Gtin = trimmedGtin,
                Name = req.Name.Trim(),
                PointsCost = req.PointsCost,
                Attributes = ToJson(req.Attributes),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.RewardProducts.Add(product);
        }
        else
        {
            product.Name = req.Name.Trim();
            product.PointsCost = req.PointsCost;
            product.Gtin = trimmedGtin;
            product.Attributes = ToJson(req.Attributes);
            product.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (req.InventoryQuantity.HasValue)
        {
            var existing = await _db.RewardInventories
                .FirstOrDefaultAsync(i => i.RewardProductId == product.Id, ct);

            if (existing is null)
            {
                _db.RewardInventories.Add(new RewardInventory
                {
                    RewardProductId = product.Id,
                    AvailableQuantity = req.InventoryQuantity.Value,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    LastSyncedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.AvailableQuantity = req.InventoryQuantity.Value;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                existing.LastSyncedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private static void Validate(RewardProductUpsertRequest req)
    {
        if (req.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.");
        if (string.IsNullOrWhiteSpace(req.RewardVendor)) throw new ArgumentException("RewardVendor is required.");
        if (string.IsNullOrWhiteSpace(req.Sku)) throw new ArgumentException("Sku is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Name is required.");
        if (req.PointsCost < 0) throw new ArgumentException("PointsCost cannot be negative.");
        if (req.InventoryQuantity is < 0) throw new ArgumentException("InventoryQuantity cannot be negative.");
    }

    private static JsonObject ToJson(Dictionary<string, object?>? dict)
    {
        if (dict is null || dict.Count == 0) return new JsonObject();
        var json = new JsonObject();
        foreach (var kvp in dict)
        {
            json[kvp.Key] = kvp.Value switch
            {
                null => null,
                string s => s,
                int i => i,
                long l => l,
                decimal d => d,
                double db => db,
                bool b => b,
                _ => JsonValue.Create(kvp.Value?.ToString())
            };
        }
        return json;
    }
}
