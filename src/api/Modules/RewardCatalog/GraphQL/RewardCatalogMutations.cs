using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Loyalty.Api.Modules.RewardCatalog.Domain;
using Loyalty.Api.Modules.RewardCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RewardCatalog.GraphQL;

/// <summary>Reward catalog mutations.</summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class RewardCatalogMutations
{
    public async Task<RewardProduct?> UpsertRewardProduct(
        UpsertRewardProductInput input,
        [Service] RewardCatalogService catalog,
        [Service] RewardCatalogDbContext db,
        CancellationToken ct)
    {
        var attributes = ToAttributes(input.Attributes);

        var request = new RewardProductUpsertRequest
        {
            TenantId = input.TenantId,
            RewardVendor = input.RewardVendor,
            Sku = input.Sku,
            Gtin = input.Gtin,
            Name = input.Name,
            PointsCost = input.PointsCost,
            InventoryQuantity = input.InventoryQuantity,
            Attributes = attributes
        };

        await catalog.UpsertAsync(new[] { request }, ct);

        return await db.RewardProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.TenantId == input.TenantId &&
                p.RewardVendor == input.RewardVendor &&
                p.Sku == input.Sku &&
                p.Gtin == input.Gtin, ct);
    }

    public async Task<bool> DeleteRewardProduct(Guid tenantId, Guid id, [Service] RewardCatalogService catalog, CancellationToken ct)
    {
        await catalog.DeleteAsync(tenantId, id, ct);
        return true;
    }

    private static Dictionary<string, object?>? ToAttributes(List<RewardProductAttributeInput>? attrs)
    {
        if (attrs is null || attrs.Count == 0) return null;

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var attr in attrs)
        {
            if (string.IsNullOrWhiteSpace(attr.Key)) continue;
            dict[attr.Key.Trim()] = ParseAttributeValue(attr.Value);
        }
        return dict;
    }

    private static object? ParseAttributeValue(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return string.Empty;

        if (bool.TryParse(trimmed, out var b)) return b;
        if (int.TryParse(trimmed, out var i)) return i;
        if (decimal.TryParse(trimmed, out var d)) return d;
        return trimmed;
    }
}

public record UpsertRewardProductInput(
    Guid TenantId,
    string RewardVendor,
    string Sku,
    string? Gtin,
    string Name,
    int PointsCost,
    int? InventoryQuantity,
    List<RewardProductAttributeInput>? Attributes);

public record RewardProductAttributeInput(string Key, string? Value);
