using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Loyalty.Api.Modules.RewardOrders.Domain;
using Loyalty.Api.Modules.RewardOrders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RewardOrders.Application;

/// <summary>Places and tracks reward redemption orders.</summary>
public class RewardOrderService
{
    private readonly RewardOrdersDbContext _db;
    private readonly IRewardCatalogLookup _catalog;
    private readonly IRewardInventoryService _inventory;
    private readonly ILedgerService _ledger;
    private readonly ICustomerLookup _customers;
    private readonly IRewardOrderDispatcher _dispatcher;

    public RewardOrderService(
        RewardOrdersDbContext db,
        IRewardCatalogLookup catalog,
        IRewardInventoryService inventory,
        ILedgerService ledger,
        ICustomerLookup customers,
        IRewardOrderDispatcher dispatcher)
    {
        _db = db;
        _catalog = catalog;
        _inventory = inventory;
        _ledger = ledger;
        _customers = customers;
        _dispatcher = dispatcher;
    }

    public Task<List<RewardOrder>> ListByCustomerAsync(Guid customerId, int take = 200, CancellationToken ct = default) =>
        _db.RewardOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<List<RewardOrder>> ListByTenantAsync(Guid tenantId, int take = 200, CancellationToken ct = default) =>
        _db.RewardOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<RewardOrder> PlaceOrderAsync(PlaceRewardOrderRequest request, bool placedOnBehalf, CancellationToken ct = default)
    {
        Validate(request);

        if (!await _customers.BelongsToTenantAsync(request.CustomerId, request.TenantId, ct))
            throw new ArgumentException("Customer does not belong to tenant.");

        var items = request.Items.GroupBy(i => i.RewardProductId)
            .Select(g => new RewardOrderLineRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        var productIds = items.Select(i => i.RewardProductId).Distinct().ToList();
        var products = await _catalog.GetByIdsAsync(productIds, ct);

        if (products.Count != productIds.Count)
            throw new ArgumentException("One or more reward products not found.");

        foreach (var line in items)
        {
            await _inventory.ReserveAsync(line.RewardProductId, line.Quantity, ct);
        }

        var order = new RewardOrder
        {
            TenantId = request.TenantId,
            CustomerId = request.CustomerId,
            ActorUserId = request.ActorUserId,
            Status = RewardOrderStatus.PendingDispatch,
            PlacedOnBehalf = placedOnBehalf,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        foreach (var line in items)
        {
            var product = products.Single(p => p.Id == line.RewardProductId);
            var totalPoints = product.PointsCost * line.Quantity;
            order.TotalPoints += totalPoints;
            order.Items.Add(new RewardOrderItem
            {
                RewardProductId = product.Id,
                RewardVendor = product.RewardVendor,
                Sku = product.Sku,
                Name = product.Name,
                Quantity = line.Quantity,
                PointsCost = product.PointsCost,
                TotalPoints = totalPoints
            });
        }

        _db.RewardOrders.Add(order);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _ledger.RedeemAsync(
                new RedeemPointsCommand(order.CustomerId, order.ActorUserId, order.TotalPoints, PointsReasons.RewardRedeem, order.Id.ToString()),
                ct);

            await _dispatcher.EnqueueAsync(order, ct);
            return order;
        }
        catch
        {
            order.Status = RewardOrderStatus.Failed;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            foreach (var line in items)
            {
                await _inventory.ReleaseAsync(line.RewardProductId, line.Quantity, ct);
            }

            throw;
        }
    }

    private static void Validate(PlaceRewardOrderRequest request)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.");
        if (request.CustomerId == Guid.Empty) throw new ArgumentException("CustomerId is required.");
        if (request.ActorUserId == Guid.Empty) throw new ArgumentException("ActorUserId is required.");
        if (request.Items.Count == 0) throw new ArgumentException("At least one item is required.");
        if (request.Items.Any(i => i.Quantity <= 0)) throw new ArgumentException("Quantity must be greater than 0.");
    }
}
