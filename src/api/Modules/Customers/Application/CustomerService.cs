using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.Customers.Application;

/// <summary>Command for creating a customer/outlet.</summary>
public record CreateCustomerCommand(Guid TenantId, string Name, string? ContactEmail, string? ExternalId);

/// <summary>Customer module application contract.</summary>
public interface ICustomerService
{
    /// <summary>Fetch a single customer with cached balance.</summary>
    Task<Customer?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>List customers for a tenant.</summary>
    Task<List<Customer>> ListByTenantAsync(Guid tenantId, int take = 500, CancellationToken ct = default);

    /// <summary>Create a customer and seed its points account.</summary>
    Task<Customer> CreateAsync(CreateCustomerCommand command, CancellationToken ct = default);
}

/// <summary>
/// Customer module application service (profile + cached balance wiring).
/// </summary>
public class CustomerService : ICustomerService, ICustomerLookup
{
    private readonly CustomersDbContext _db;
    private readonly LedgerDbContext _ledgerDb;

    /// <summary>Constructs the service with module DbContexts.</summary>
    public CustomerService(CustomersDbContext db, LedgerDbContext ledgerDb)
    {
        _db = db;
        _ledgerDb = ledgerDb;
    }

    /// <inheritdoc />
    public Task<Customer?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Customers
           .Include(c => c.PointsAccount)
           .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public Task<List<Customer>> ListByTenantAsync(Guid tenantId, int take = 500, CancellationToken ct = default) =>
        _db.Customers
           .Where(c => c.TenantId == tenantId)
           .Include(c => c.PointsAccount)
           .OrderBy(c => c.Name)
           .Take(take)
           .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<Customer> CreateAsync(CreateCustomerCommand command, CancellationToken ct = default)
    {
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == command.TenantId, ct);
        if (!tenantExists)
            throw new Exception("Tenant not found.");

        var name = command.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new Exception("Customer name is required.");

        var contactEmail = command.ContactEmail?.Trim();
        var externalId = command.ExternalId?.Trim();

        var customer = new Customer
        {
            TenantId = command.TenantId,
            Name = name,
            ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail,
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId
        };

        _db.Customers.Add(customer);

        // Create cached balance record immediately.
        _ledgerDb.PointsAccounts.Add(new PointsAccount
        {
            CustomerId = customer.Id,
            Balance = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await _ledgerDb.SaveChangesAsync(ct);
        return customer;
    }

    /// <inheritdoc />
    public Task<bool> BelongsToTenantAsync(Guid customerId, Guid tenantId, CancellationToken ct = default) =>
        _db.Customers.AnyAsync(c => c.Id == customerId && c.TenantId == tenantId, ct);
}
