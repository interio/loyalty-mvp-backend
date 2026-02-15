namespace Loyalty.Api.Modules.Products.Domain;

/// <summary>
/// Distributor company that provides products for customers inside a tenant scope.
/// </summary>
public class Distributor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Product> Products { get; set; } = new();
}
