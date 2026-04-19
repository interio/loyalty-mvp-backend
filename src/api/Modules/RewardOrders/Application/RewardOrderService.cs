using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.RewardCatalog.Application;
using Loyalty.Api.Modules.RewardOrders.Domain;
using Loyalty.Api.Modules.RewardOrders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Shared;

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

    public async Task<PageResult<RewardOrder>> ListByTenantPageAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var query = _db.RewardOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAt);

        return await query.ToPageResultAsync(page, pageSize, ct);
    }

    public async Task<CursorPageResult<RewardOrder>> ListByTenantCursorAsync(Guid tenantId, int take, string? after, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var size = Math.Clamp(take, 1, 200);
        var cursor = CursorPaging.DecodeTimestampCursor(after);

        var query = _db.RewardOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenantId);

        if (cursor != null)
        {
            query = query.Where(o =>
                o.CreatedAt < cursor.Timestamp ||
                (o.CreatedAt == cursor.Timestamp && o.Id.CompareTo(cursor.Id) < 0));
        }

        var rows = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Take(size + 1)
            .ToListAsync(ct);

        var hasNext = rows.Count > size;
        var items = hasNext ? rows.Take(size).ToList() : rows;
        var endCursor = items.Count == 0
            ? null
            : CursorPaging.EncodeTimestampCursor(items[^1].CreatedAt, items[^1].Id);

        return new CursorPageResult<RewardOrder>(items, endCursor, hasNext);
    }

    public async Task<List<RewardOrderSummary>> ListSummaryByTenantRangeAsync(
        Guid tenantId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");
        if (toExclusive <= fromInclusive) throw new ArgumentException("to must be greater than from.");

        return await _db.RewardOrders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.CreatedAt >= fromInclusive && o.CreatedAt < toExclusive)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new RewardOrderSummary
            {
                Id = o.Id,
                CreatedAt = o.CreatedAt,
                TotalPoints = o.TotalPoints,
                Status = o.Status
            })
            .ToListAsync(ct);
    }

    public Task<RewardOrder?> GetByIdAsync(Guid tenantId, Guid orderId, CancellationToken ct = default) =>
        _db.RewardOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId, ct);

    public async Task<RewardOrder> UpdateStatusAsync(Guid tenantId, Guid orderId, RewardOrderStatus status, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.");

        var order = await _db.RewardOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId, ct);

        if (order is null)
            throw new System.Collections.Generic.KeyNotFoundException("Reward order not found for tenant.");

        if (order.Status == status) return order;

        order.Status = status;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<RewardOrder> PlaceOrderAsync(PlaceRewardOrderRequest request, bool placedOnBehalf, CancellationToken ct = default)
    {
        Validate(request);

        if (!await _customers.BelongsToTenantAsync(request.CustomerId, request.TenantId, ct))
            throw new ArgumentException("Customer does not belong to tenant.");

        var items = request.Items.GroupBy(i => i.RewardProductId)
            .Select(g => new RewardOrderLineRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        var productIds = items.Select(i => i.RewardProductId).Distinct().ToList();
        var products = await _catalog.GetByIdsAsync(request.TenantId, productIds, ct);

        if (products.Count != productIds.Count)
            throw new ArgumentException("One or more reward products not found.");

        var reserved = new List<RewardOrderLineRequest>();
        var releaseAttempted = false;
        try
        {
            foreach (var line in items)
            {
                await _inventory.ReserveAsync(request.TenantId, line.RewardProductId, line.Quantity, ct);
                reserved.Add(line);
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
            catch (Exception ex)
            {
                order.Status = RewardOrderStatus.Failed;
                order.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);

                releaseAttempted = true;
                await ReleaseReservedAsync(request.TenantId, reserved, ct, ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            if (!releaseAttempted)
            {
                releaseAttempted = true;
                await ReleaseReservedAsync(request.TenantId, reserved, ct, ex);
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

    private async Task ReleaseReservedAsync(Guid tenantId, List<RewardOrderLineRequest> reserved, CancellationToken ct, Exception? original)
    {
        if (reserved.Count == 0) return;
        try
        {
            foreach (var line in reserved)
            {
                await _inventory.ReleaseAsync(tenantId, line.RewardProductId, line.Quantity, ct);
            }
        }
        catch (Exception releaseEx)
        {
            if (original != null)
                throw new AggregateException(original, releaseEx);
            throw;
        }
        finally
        {
            reserved.Clear();
        }
    }
}

public class RewardOrderSummary
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int TotalPoints { get; set; }
    public RewardOrderStatus Status { get; set; }
}
