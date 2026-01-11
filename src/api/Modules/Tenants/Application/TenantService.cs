using Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.Tenants.Application;

/// <summary>Tenant module application contract.</summary>
public interface ITenantService
{
    /// <summary>Create a tenant.</summary>
    Task<Tenant> CreateAsync(string name, CancellationToken ct = default);

    /// <summary>Check if a tenant exists.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>List tenants for administration.</summary>
    Task<List<Tenant>> ListAsync(int take = 200, CancellationToken ct = default);

    /// <summary>Page tenants for administration.</summary>
    Task<TenantPageResult> ListPageAsync(int page, int pageSize, CancellationToken ct = default);
}

/// <summary>
/// Tenant module application service. Keeps creation and queries in one place for easy extraction later.
/// </summary>
public class TenantService : ITenantService
{
    private readonly TenantsDbContext _db;

    /// <summary>Constructs the tenant service.</summary>
    public TenantService(TenantsDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<Tenant> CreateAsync(string name, CancellationToken ct = default)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new Exception("Tenant name is required.");

        var tenant = new Tenant { Name = trimmed };
        _db.Tenants.Add(tenant);

        await _db.SaveChangesAsync(ct);
        return tenant;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        _db.Tenants.AnyAsync(t => t.Id == id, ct);

    /// <inheritdoc />
    public Task<List<Tenant>> ListAsync(int take = 200, CancellationToken ct = default) =>
        _db.Tenants
           .AsNoTracking()
           .OrderBy(t => t.Name)
           .Take(take)
           .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<TenantPageResult> ListPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var size = Math.Clamp(pageSize, 1, 200);
        var safePage = Math.Max(page, 1);

        var baseQuery = _db.Tenants.AsNoTracking();
        var totalCount = await baseQuery.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)size);
        if (totalPages > 0 && safePage > totalPages)
        {
            safePage = totalPages;
        }

        var items = await baseQuery
            .OrderBy(t => t.Name)
            .Skip((safePage - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new TenantPageResult(items, totalCount, safePage, size, totalPages);
    }
}

public record TenantPageResult(
    IReadOnlyList<Tenant> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
