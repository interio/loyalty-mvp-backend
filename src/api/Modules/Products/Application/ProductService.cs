using System.Text.Json.Nodes;
using Loyalty.Api.Modules.Products.Domain;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.Products.Application;

/// <summary>Product ingestion service (ERP-backed catalog per distributor).</summary>
public class ProductService
{
    private readonly ProductsDbContext _db;

    public ProductService(ProductsDbContext db) => _db = db;

    /// <summary>Upserts a batch of products.</summary>
    public async Task UpsertAsync(IEnumerable<ProductUpsertRequest> requests, CancellationToken ct = default)
    {
        foreach (var req in requests)
        {
            await UpsertSingleAsync(req, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertSingleAsync(ProductUpsertRequest req, CancellationToken ct)
    {
        Validate(req);

        var product = await _db.Products
            .FirstOrDefaultAsync(p =>
                p.DistributorId == req.DistributorId &&
                p.Sku == req.Sku &&
                p.Gtin == req.Gtin, ct);

        if (product is null)
        {
            product = new Product
            {
                DistributorId = req.DistributorId,
                Sku = req.Sku.Trim(),
                Gtin = string.IsNullOrWhiteSpace(req.Gtin) ? null : req.Gtin.Trim(),
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
            product.Attributes = ToJson(req.Attributes);
            product.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static void Validate(ProductUpsertRequest req)
    {
        if (req.DistributorId == Guid.Empty) throw new ArgumentException("DistributorId is required.");
        if (string.IsNullOrWhiteSpace(req.Sku)) throw new ArgumentException("Sku is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ArgumentException("Name is required.");
        if (req.Cost < 0) throw new ArgumentException("Cost cannot be negative.");
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
