using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;
using System.Transactions;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.Customers.Application;

/// <summary>Command for creating a customer/outlet.</summary>
public record CreateCustomerCommand(
    Guid TenantId,
    string Name,
    string? ContactEmail,
    string? ExternalId,
    string? Tier = null,
    CustomerAddress? Address = null,
    string? PhoneNumber = null,
    string? Type = null,
    string? BusinessSegment = null,
    DateTimeOffset? OnboardDate = null,
    int? Status = null);

/// <summary>Customer module application contract.</summary>
public interface ICustomerService
{
    /// <summary>Fetch a single customer with cached balance.</summary>
    Task<Customer?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>List customers for a tenant.</summary>
    Task<List<Customer>> ListByTenantAsync(Guid tenantId, int take = 500, CancellationToken ct = default);

    /// <summary>Search customers for a tenant using Postgres full-text search.</summary>
    Task<List<Customer>> SearchByTenantAsync(Guid tenantId, string search, int take = 100, CancellationToken ct = default);

    /// <summary>Page customers for a tenant using classic page numbers.</summary>
    Task<PageResult<Customer>> ListByTenantPageAsync(Guid tenantId, int page, int pageSize, string? search = null, CancellationToken ct = default);

    /// <summary>Create a customer and seed its points account.</summary>
    Task<Customer> CreateAsync(CreateCustomerCommand command, CancellationToken ct = default);

    /// <summary>Updates loyalty tier for an existing customer.</summary>
    Task<Customer> UpdateTierAsync(Guid customerId, Guid tenantId, string tier, CancellationToken ct = default);
}

/// <summary>
/// Customer module application service (profile + cached balance wiring).
/// </summary>
    public class CustomerService : ICustomerService, ICustomerLookup
    {
        private readonly CustomersDbContext _db;
        private readonly LedgerDbContext _ledgerDb;
        private bool ShouldShareTransaction => _db.Database.IsRelational() && _ledgerDb.Database.IsRelational();

        /// <summary>Constructs the service with module DbContexts.</summary>
        public CustomerService(CustomersDbContext db, LedgerDbContext ledgerDb)
        {
            _db = db;
        _ledgerDb = ledgerDb;
    }

    /// <inheritdoc />
    public Task<Customer?> GetAsync(Guid id, CancellationToken ct = default) =>
        LoadPointsAsync(
            _db.Customers
               .AsNoTracking()
               .Where(c => c.Id == id),
            ct)
        .ContinueWith(t => t.Result.FirstOrDefault(), ct);

    /// <inheritdoc />
    public Task<List<Customer>> ListByTenantAsync(Guid tenantId, int take = 500, CancellationToken ct = default) =>
        LoadPointsAsync(
            _db.Customers
               .AsNoTracking()
               .Where(c => c.TenantId == tenantId)
               .OrderBy(c => c.Name)
               .Take(take),
            ct);

    /// <inheritdoc />
    public Task<List<Customer>> SearchByTenantAsync(Guid tenantId, string search, int take = 100, CancellationToken ct = default)
    {
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term)) return Task.FromResult(new List<Customer>());

        var baseQuery = _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .Where(c =>
                EF.Functions.ToTsVector(
                        "simple",
                        (c.Name ?? string.Empty) + " " +
                        (c.ExternalId ?? string.Empty) + " " +
                        (c.ContactEmail ?? string.Empty) + " " +
                        (c.PhoneNumber ?? string.Empty) + " " +
                        (c.Type ?? string.Empty) + " " +
                        (c.BusinessSegment ?? string.Empty))
                    .Matches(EF.Functions.PlainToTsQuery("simple", term)))
            .OrderBy(c => c.Name)
            .Take(take);

        return LoadPointsAsync(baseQuery, ct);
    }

    /// <inheritdoc />
    public async Task<PageResult<Customer>> ListByTenantPageAsync(Guid tenantId, int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var baseQuery = _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId);

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            baseQuery = baseQuery.Where(c =>
                EF.Functions.ToTsVector(
                        "simple",
                        (c.Name ?? string.Empty) + " " +
                        (c.ExternalId ?? string.Empty) + " " +
                        (c.ContactEmail ?? string.Empty) + " " +
                        (c.PhoneNumber ?? string.Empty) + " " +
                        (c.Type ?? string.Empty) + " " +
                        (c.BusinessSegment ?? string.Empty))
                    .Matches(EF.Functions.PlainToTsQuery("simple", term)));
        }

        var size = Math.Clamp(pageSize, 1, 200);
        var safePage = Math.Max(page, 1);
        var totalCount = await baseQuery.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)size);
        if (totalPages > 0 && safePage > totalPages)
        {
            safePage = totalPages;
        }

        var query = baseQuery
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip((safePage - 1) * size)
            .Take(size);

        var items = await LoadPointsAsync(query, ct);

        return new PageResult<Customer>(items, totalCount, safePage, size, totalPages);
    }

    /// <inheritdoc />
    public async Task<Customer> CreateAsync(CreateCustomerCommand command, CancellationToken ct = default)
    {
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == command.TenantId, ct);
        if (!tenantExists)
            throw new Exception("Tenant not found.");

        var name = command.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new Exception("Customer name is required.");

        var contactEmail = TrimOrNull(command.ContactEmail);
        var externalId = TrimOrNull(command.ExternalId);
        var phoneNumber = TrimOrNull(command.PhoneNumber);
        var customerType = TrimOrNull(command.Type);
        var businessSegment = TrimOrNull(command.BusinessSegment);
        var address = NormalizeAddress(command.Address);
        var onboardDate = command.OnboardDate ?? DateTimeOffset.UtcNow;

        var tier = CustomerTierCatalog.NormalizeOrDefault(command.Tier);
        if (!CustomerTierCatalog.IsSupported(tier))
            throw new Exception("Customer tier must be one of: bronze, silver, gold, platinum.");

        var status = command.Status ?? CustomerStatusCatalog.Active;
        if (!CustomerStatusCatalog.IsSupported(status))
            throw new Exception("Customer status must be one of: 0 (inactive), 1 (active), 2 (suspended).");

        var customer = new Customer
        {
            TenantId = command.TenantId,
            Name = name,
            ContactEmail = contactEmail,
            ExternalId = externalId,
            Tier = tier,
            Address = address,
            PhoneNumber = phoneNumber,
            Type = customerType,
            BusinessSegment = businessSegment,
            OnboardDate = onboardDate,
            Status = status
        };

        if (ShouldShareTransaction)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

                _db.Customers.Add(customer);
                _ledgerDb.PointsAccounts.Add(new PointsAccount
                {
                    CustomerId = customer.Id,
                    Balance = 0,
                    UpdatedAt = DateTimeOffset.UtcNow
                });

                await _db.SaveChangesAsync(ct);
                await _ledgerDb.SaveChangesAsync(ct);

                scope.Complete();
                return customer;
            });
        }

        _db.Customers.Add(customer);
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

    /// <inheritdoc />
    public async Task<Customer> UpdateTierAsync(Guid customerId, Guid tenantId, string tier, CancellationToken ct = default)
    {
        if (customerId == Guid.Empty) throw new Exception("Customer id is required.");
        if (tenantId == Guid.Empty) throw new Exception("Tenant id is required.");

        var normalizedTier = CustomerTierCatalog.NormalizeOrDefault(tier);
        if (!CustomerTierCatalog.IsSupported(normalizedTier))
            throw new Exception("Customer tier must be one of: bronze, silver, gold, platinum.");

        var customer = await _db.Customers.FirstOrDefaultAsync(
            c => c.Id == customerId && c.TenantId == tenantId,
            ct);

        if (customer is null)
            throw new Exception("Customer not found for tenant.");

        if (string.Equals(customer.Tier, normalizedTier, StringComparison.OrdinalIgnoreCase))
            return customer;

        customer.Tier = normalizedTier;
        await _db.SaveChangesAsync(ct);
        return customer;
    }

    private async Task<List<Customer>> LoadPointsAsync(IQueryable<Customer> query, CancellationToken ct)
    {
        var customers = await query.ToListAsync(ct);
        if (customers.Count == 0) return customers;

        var ids = customers.Select(c => c.Id).ToList();
        var accounts = await _ledgerDb.PointsAccounts
            .AsNoTracking()
            .Where(a => ids.Contains(a.CustomerId))
            .ToDictionaryAsync(a => a.CustomerId, ct);

        foreach (var c in customers)
        {
            if (accounts.TryGetValue(c.Id, out var acct))
            {
                c.PointsAccount = acct;
            }
        }

        return customers;
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static CustomerAddress? NormalizeAddress(CustomerAddress? value)
    {
        if (value is null) return null;

        var normalized = new CustomerAddress
        {
            Address = TrimOrNull(value.Address),
            CountryCode = TrimOrNull(value.CountryCode)?.ToUpperInvariant(),
            PostalCode = TrimOrNull(value.PostalCode),
            Region = TrimOrNull(value.Region)
        };

        if (normalized.Address is null &&
            normalized.CountryCode is null &&
            normalized.PostalCode is null &&
            normalized.Region is null)
        {
            return null;
        }

        return normalized;
    }

}
