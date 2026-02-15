using Loyalty.Api.Modules.Products.Domain;
using Loyalty.Api.Modules.Products.Infrastructure.Persistence;
using Loyalty.Api.Modules.Shared;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.Products.Application;

public record CreateDistributorCommand(Guid TenantId, string Name, string DisplayName);

/// <summary>Manages tenant-scoped distributors.</summary>
public class DistributorService
{
    private readonly ProductsDbContext _db;

    public DistributorService(ProductsDbContext db) => _db = db;

    public Task<List<Distributor>> ListByTenantAsync(Guid tenantId, int take = 500, CancellationToken ct = default)
    {
        EnsureTenantId(tenantId);
        return _db.Distributors
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderBy(d => d.DisplayName)
            .ThenBy(d => d.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<PageResult<Distributor>> ListByTenantPageAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? search = null,
        CancellationToken ct = default)
    {
        EnsureTenantId(tenantId);

        var query = _db.Distributors
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId);

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            if (_db.Database.IsRelational())
            {
                var pattern = $"%{term}%";
                query = query.Where(d =>
                    EF.Functions.ILike(d.Name, pattern) ||
                    EF.Functions.ILike(d.DisplayName, pattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(d =>
                    d.Name.ToLower().Contains(lowered) ||
                    d.DisplayName.ToLower().Contains(lowered));
            }
        }

        query = query.OrderBy(d => d.DisplayName).ThenBy(d => d.Name);
        return await query.ToPageResultAsync(page, pageSize, ct);
    }

    public Task<List<Distributor>> SearchByTenantAsync(Guid tenantId, string search, int take = 200, CancellationToken ct = default)
    {
        EnsureTenantId(tenantId);
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term)) return Task.FromResult(new List<Distributor>());

        var query = _db.Distributors
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId);

        if (_db.Database.IsRelational())
        {
            var pattern = $"%{term}%";
            query = query.Where(d =>
                EF.Functions.ILike(d.Name, pattern) ||
                EF.Functions.ILike(d.DisplayName, pattern));
        }
        else
        {
            var lowered = term.ToLowerInvariant();
            query = query.Where(d =>
                d.Name.ToLower().Contains(lowered) ||
                d.DisplayName.ToLower().Contains(lowered));
        }

        return query
            .OrderBy(d => d.DisplayName)
            .ThenBy(d => d.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<Distributor> CreateAsync(CreateDistributorCommand command, CancellationToken ct = default)
    {
        EnsureTenantId(command.TenantId);

        var name = command.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.");

        var displayName = command.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName is required.");

        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == command.TenantId, ct);
        if (!tenantExists)
            throw new ArgumentException("Tenant not found.");

        var exists = await _db.Distributors.AnyAsync(
            d => d.TenantId == command.TenantId && d.Name == name,
            ct);
        if (exists)
            throw new ArgumentException("Distributor name already exists for this tenant.");

        var distributor = new Distributor
        {
            TenantId = command.TenantId,
            Name = name,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Distributors.Add(distributor);
        await _db.SaveChangesAsync(ct);
        return distributor;
    }

    private static void EnsureTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.");
    }
}
