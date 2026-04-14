namespace Loyalty.Api.Modules.Tenants.Domain;

/// <summary>Generic tenant/default configuration key-value entry.</summary>
public class TenantConfigSetting
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tenant scope for this value; null means shared default.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>Owning tenant navigation when tenant-scoped.</summary>
    public Tenant? Tenant { get; set; }

    /// <summary>Config key name (for example "currency").</summary>
    public string ConfigName { get; set; } = default!;

    /// <summary>Config value payload.</summary>
    public string ConfigValue { get; set; } = default!;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
