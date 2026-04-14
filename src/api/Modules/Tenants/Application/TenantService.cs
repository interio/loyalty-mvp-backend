using Loyalty.Api.Modules.Tenants.Infrastructure.Persistence;
using Loyalty.Api.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.Tenants.Application;

/// <summary>Tenant configuration payload for currently supported tenant-level settings.</summary>
public record TenantConfigCommand(string? Currency = null);

/// <summary>Tenant module application contract.</summary>
public interface ITenantService
{
    /// <summary>Create a tenant.</summary>
    Task<Tenant> CreateAsync(
        string name,
        string? email = null,
        string? phone = null,
        string? address = null,
        TenantConfigCommand? config = null,
        CancellationToken ct = default);

    /// <summary>Update tenant configuration values.</summary>
    Task<Tenant> UpdateConfigAsync(Guid tenantId, TenantConfigCommand config, CancellationToken ct = default);

    /// <summary>
    /// Upserts a configuration value.
    /// Use <c>tenantId = null</c> for a shared default value.
    /// </summary>
    Task SetConfigValueAsync(Guid? tenantId, string configName, string configValue, CancellationToken ct = default);

    /// <summary>
    /// Resolves a tenant config value with fallback to shared default.
    /// </summary>
    Task<string?> GetConfigValueAsync(Guid tenantId, string configName, CancellationToken ct = default);

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
        TenantConfigCommand? config = null,
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
            Address = NormalizeOptional(address)
        };
        _db.Tenants.Add(tenant);

        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(config?.Currency))
        {
            await SetConfigValueAsync(tenant.Id, TenantConfigNames.Currency, config!.Currency!, ct);
        }

        return tenant;
    }

    /// <inheritdoc />
    public async Task<Tenant> UpdateConfigAsync(Guid tenantId, TenantConfigCommand config, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id is required.");
        if (string.IsNullOrWhiteSpace(config.Currency))
            throw new ArgumentException("Currency is required.");

        await SetConfigValueAsync(tenantId, TenantConfigNames.Currency, config.Currency!, ct);
        return await _db.Tenants.FirstAsync(t => t.Id == tenantId, ct);
    }

    /// <inheritdoc />
    public async Task SetConfigValueAsync(Guid? tenantId, string configName, string configValue, CancellationToken ct = default)
    {
        var normalizedName = NormalizeConfigName(configName);
        var normalizedValue = NormalizeConfigValue(normalizedName, configValue);

        if (tenantId.HasValue && tenantId.Value == Guid.Empty)
            throw new ArgumentException("Tenant id is invalid.");

        if (tenantId.HasValue)
        {
            var exists = await _db.Tenants.AnyAsync(t => t.Id == tenantId.Value, ct);
            if (!exists)
                throw new System.Collections.Generic.KeyNotFoundException("Tenant not found.");
        }

        var setting = await _db.TenantConfigSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ConfigName == normalizedName, ct);

        if (setting is null)
        {
            setting = new TenantConfigSetting
            {
                TenantId = tenantId,
                ConfigName = normalizedName,
                ConfigValue = normalizedValue,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.TenantConfigSettings.Add(setting);
        }
        else
        {
            setting.ConfigValue = normalizedValue;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<string?> GetConfigValueAsync(Guid tenantId, string configName, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id is required.");

        var normalizedName = NormalizeConfigName(configName);

        var tenantValue = await _db.TenantConfigSettings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ConfigName == normalizedName)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(tenantValue))
            return tenantValue;

        return await _db.TenantConfigSettings
            .AsNoTracking()
            .Where(x => x.TenantId == null && x.ConfigName == normalizedName)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);
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

    private static string NormalizeCurrency(string? currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Currency is required.");

        if (normalized.Length != 3 || normalized.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Currency must be a valid ISO 4217 code (3 letters), for example EUR or USD.");

        return normalized;
    }

    private static string NormalizeConfigName(string? configName)
    {
        var normalized = configName?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Config name is required.");

        if (normalized.Length > 120)
            throw new ArgumentException("Config name length must be 120 characters or fewer.");

        return normalized;
    }

    private static string NormalizeConfigValue(string normalizedConfigName, string? configValue)
    {
        if (normalizedConfigName == TenantConfigNames.Currency)
            return NormalizeCurrency(configValue);

        var normalized = configValue?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Config value is required.");

        if (normalized.Length > 2000)
            throw new ArgumentException("Config value length must be 2000 characters or fewer.");

        return normalized;
    }
}
