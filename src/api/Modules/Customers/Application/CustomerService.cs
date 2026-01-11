using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;
using System.Transactions;
using Npgsql.EntityFrameworkCore.PostgreSQL;

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

    /// <summary>Search customers for a tenant using Postgres full-text search.</summary>
    Task<List<Customer>> SearchByTenantAsync(Guid tenantId, string search, int take = 100, CancellationToken ct = default);

    /// <summary>Page customers for a tenant using classic page numbers.</summary>
    Task<CustomerPageResult> ListByTenantPageAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);

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
                        (c.Name ?? string.Empty) + " " + (c.ExternalId ?? string.Empty) + " " + (c.ContactEmail ?? string.Empty))
                    .Matches(EF.Functions.PlainToTsQuery("simple", term)))
            .OrderBy(c => c.Name)
            .Take(take);

        return LoadPointsAsync(baseQuery, ct);
    }

    /// <inheritdoc />
    public async Task<CustomerPageResult> ListByTenantPageAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var size = Math.Clamp(pageSize, 1, 200);
        var safePage = Math.Max(page, 1);

        var baseQuery = _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId);

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

        return new CustomerPageResult(items, totalCount, safePage, size, totalPages);
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

        var contactEmail = command.ContactEmail?.Trim();
            var externalId = command.ExternalId?.Trim();

            var customer = new Customer
            {
                TenantId = command.TenantId,
            Name = name,
            ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail,
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId
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

}

public record CustomerPageResult(
    IReadOnlyList<Customer> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
