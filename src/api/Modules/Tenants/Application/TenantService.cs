using Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.Tenants.Application;

/// <summary>Tenant module application contract.</summary>
public interface ITenantService
{
    /// <summary>Create a tenant.</summary>
    Task<Tenant> CreateAsync(
        string name,
        string? email = null,
        string? phone = null,
        string? address = null,
        CancellationToken ct = default);

    /// <summary>Check if a tenant exists.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>List tenants for administration.</summary>
    Task<List<Tenant>> ListAsync(int take = 200, CancellationToken ct = default);

    /// <summary>Page tenants for administration.</summary>
    Task<PageResult<Tenant>> ListPageAsync(int page, int pageSize, CancellationToken ct = default);
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
    public async Task<Tenant> CreateAsync(
        string name,
        string? email = null,
        string? phone = null,
        string? address = null,
        CancellationToken ct = default)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new Exception("Tenant name is required.");

        var tenant = new Tenant
        {
            Name = trimmed,
            Email = NormalizeOptional(email),
            Phone = NormalizeOptional(phone),
            Address = NormalizeOptional(address),
        };
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
    public async Task<PageResult<Tenant>> ListPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name);

        return await query.ToPageResultAsync(page, pageSize, ct);
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
