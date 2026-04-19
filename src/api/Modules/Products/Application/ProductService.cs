using System.Text.Json.Nodes;
using Loyalty.Api.Modules.Products.Domain;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.Products.Application;

/// <summary>Product ingestion service (ERP-backed catalog per tenant/distributor).</summary>
public class ProductService
{
    private readonly ProductsDbContext _db;

    public ProductService(ProductsDbContext db) => _db = db;

    /// <summary>List products.</summary>
    public Task<List<Product>> ListAsync(Guid tenantId, int take = 500, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        return _db.Products
            .AsNoTracking()
            .Include(p => p.Distributor)
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>Page products.</summary>
    public async Task<PageResult<Product>> ListPageAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? search = null,
        CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Distributor)
            .Where(p => p.TenantId == tenantId);

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Sku, pattern) ||
                (p.Gtin != null && EF.Functions.ILike(p.Gtin, pattern)));
        }

        query = query.OrderBy(p => p.Name);

        return await query.ToPageResultAsync(page, pageSize, ct);
    }

    /// <summary>Search products.</summary>
    public Task<List<Product>> SearchAsync(Guid tenantId, string search, int take = 200, CancellationToken ct = default)
    {
        EnsureTenant(tenantId);
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term)) return Task.FromResult(new List<Product>());

        var pattern = $"%{term}%";

        return _db.Products
           .AsNoTracking()
           .Include(p => p.Distributor)
           .Where(p => p.TenantId == tenantId)
           .Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Sku, pattern) ||
                (p.Gtin != null && EF.Functions.ILike(p.Gtin, pattern)))
           .OrderBy(p => p.Name)
           .Take(take)
           .ToListAsync(ct);
    }

    /// <summary>Upserts a batch of products.</summary>
    public async Task UpsertAsync(IEnumerable<ProductUpsertRequest> requests, CancellationToken ct = default)
    {
        var distributorScopeCache = new Dictionary<(Guid TenantId, Guid DistributorId), bool>();
        var distributorNameCache = new Dictionary<(Guid TenantId, string DistributorName), Guid?>();
        foreach (var req in requests)
        {
            await UpsertSingleAsync(req, distributorScopeCache, distributorNameCache, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertSingleAsync(
        ProductUpsertRequest req,
        IDictionary<(Guid TenantId, Guid DistributorId), bool> distributorScopeCache,
        IDictionary<(Guid TenantId, string DistributorName), Guid?> distributorNameCache,
        CancellationToken ct)
    {
        Validate(req);
        var distributorId = await ResolveDistributorIdAsync(req, distributorScopeCache, distributorNameCache, ct);

        var trimmedSku = req.Sku.Trim();
        var trimmedGtin = string.IsNullOrWhiteSpace(req.Gtin) ? null : req.Gtin.Trim();

        var query = _db.Products
            .Where(p =>
                p.TenantId == req.TenantId &&
                p.DistributorId == distributorId &&
                p.Sku == trimmedSku);

        if (!string.IsNullOrWhiteSpace(trimmedGtin))
            query = query.Where(p => p.Gtin == trimmedGtin);

        var product = await query.FirstOrDefaultAsync(ct);

        if (product is null)
        {
            product = new Product
            {
                TenantId = req.TenantId,
                DistributorId = distributorId,
                Sku = trimmedSku,
                Gtin = trimmedGtin,
                Name = req.Name.Trim(),
                Cost = req.Cost,
                Attributes = ToJson(req.Attributes),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Products.Add(product);
        }
        else
        {
            product.Name = req.Name.Trim();
            product.Cost = req.Cost;
            product.Gtin = trimmedGtin;
            product.Attributes = ToJson(req.Attributes);
            product.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static void Validate(ProductUpsertRequest req)
    {
        EnsureTenant(req.TenantId);
        var hasDistributorId = req.DistributorId != Guid.Empty;
        var hasDistributorName = !string.IsNullOrWhiteSpace(req.DistributorName);
        if (!hasDistributorId && !hasDistributorName)
            throw new ArgumentException("DistributorName or DistributorId is required.");

        if (string.IsNullOrWhiteSpace(req.Sku)) throw new ArgumentException("Sku is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Name is required.");
        if (req.Cost.HasValue && req.Cost.Value < 0) throw new ArgumentException("Cost cannot be negative.");
    }

    private static void EnsureTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.");
    }

    private async Task<Guid> ResolveDistributorIdAsync(
        ProductUpsertRequest req,
        IDictionary<(Guid TenantId, Guid DistributorId), bool> distributorScopeCache,
        IDictionary<(Guid TenantId, string DistributorName), Guid?> distributorNameCache,
        CancellationToken ct)
    {
        var hasDistributorName = !string.IsNullOrWhiteSpace(req.DistributorName);
        if (!hasDistributorName)
        {
            await EnsureDistributorExistsAsync(req.TenantId, req.DistributorId, distributorScopeCache, ct);
            return req.DistributorId;
        }

        var normalizedName = req.DistributorName!.Trim();
        var cacheKey = (req.TenantId, normalizedName.ToUpperInvariant());
        if (!distributorNameCache.TryGetValue(cacheKey, out var resolvedDistributorId))
        {
            var normalizedUpper = normalizedName.ToUpperInvariant();
            var matches = await _db.Distributors
                .AsNoTracking()
                .Where(d => d.TenantId == req.TenantId && d.Name.ToUpper() == normalizedUpper)
                .Select(d => d.Id)
                .ToListAsync(ct);

            resolvedDistributorId = matches.Count switch
            {
                0 => null,
                1 => matches[0],
                _ => throw new ArgumentException("Multiple distributors matched the provided distributorName for this tenant.")
            };

            distributorNameCache[cacheKey] = resolvedDistributorId;
        }

        if (!resolvedDistributorId.HasValue)
            throw new ArgumentException("Distributor not found for this tenant.");

        if (req.DistributorId != Guid.Empty && req.DistributorId != resolvedDistributorId.Value)
            throw new ArgumentException("DistributorId does not match the provided distributorName for this tenant.");

        await EnsureDistributorExistsAsync(req.TenantId, resolvedDistributorId.Value, distributorScopeCache, ct);
        return resolvedDistributorId.Value;
    }

    private async Task EnsureDistributorExistsAsync(
        Guid tenantId,
        Guid distributorId,
        IDictionary<(Guid TenantId, Guid DistributorId), bool> distributorScopeCache,
        CancellationToken ct)
    {
        var key = (tenantId, distributorId);
        if (distributorScopeCache.TryGetValue(key, out var exists))
        {
            if (!exists)
                throw new ArgumentException("Distributor not found for this tenant.");
            return;
        }

        exists = await _db.Distributors.AnyAsync(d => d.TenantId == tenantId && d.Id == distributorId, ct);
        distributorScopeCache[key] = exists;
        if (!exists)
            throw new ArgumentException("Distributor not found for this tenant.");
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
