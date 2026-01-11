using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;
using Loyalty.Api.Modules.Shared;

namespace Loyalty.Api.Modules.Customers.Application;

/// <summary>Command for creating a user under a customer.</summary>
public record CreateUserCommand(Guid TenantId, Guid CustomerId, string Email, string? Role, string? ExternalId);

/// <summary>User application contract within the Customers module.</summary>
public interface IUserService
{
    /// <summary>List users belonging to a customer.</summary>
    Task<List<User>> ListByCustomerAsync(Guid customerId, int take = 500, CancellationToken ct = default);

    /// <summary>List users belonging to a tenant.</summary>
    Task<List<User>> ListByTenantAsync(Guid tenantId, int take = 500, CancellationToken ct = default);

    /// <summary>Page users belonging to a tenant.</summary>
    Task<PageResult<User>> ListByTenantPageAsync(Guid tenantId, int page, int pageSize, string? search = null, CancellationToken ct = default);

    /// <summary>Search users within a tenant.</summary>
    Task<List<User>> SearchByTenantAsync(Guid tenantId, string search, int take = 200, CancellationToken ct = default);

    /// <summary>Create a user under a customer/tenant.</summary>
    Task<User> CreateAsync(CreateUserCommand command, CancellationToken ct = default);
}

/// <summary>
/// Users application service (employees/actors) inside the Customers module.
/// </summary>
public class UserService : IUserService, IUserLookup
{
    private readonly CustomersDbContext _db;

    /// <summary>Constructs the user service.</summary>
    public UserService(CustomersDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<List<User>> ListByCustomerAsync(Guid customerId, int take = 500, CancellationToken ct = default) =>
        _db.Users
           .AsNoTracking()
           .Include(u => u.Customer)
           .Where(u => u.CustomerId == customerId)
           .OrderBy(u => u.Email)
           .Take(take)
           .ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<User>> ListByTenantAsync(Guid tenantId, int take = 500, CancellationToken ct = default) =>
        _db.Users
           .AsNoTracking()
           .Include(u => u.Customer)
           .Where(u => u.TenantId == tenantId)
           .OrderBy(u => u.Email)
           .Take(take)
           .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<PageResult<User>> ListByTenantPageAsync(Guid tenantId, int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");

        var size = Math.Clamp(pageSize, 1, 200);
        var safePage = Math.Max(page, 1);

        var baseQuery = _db.Users
            .AsNoTracking()
            .Include(u => u.Customer)
            .Where(u => u.TenantId == tenantId);

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            baseQuery = baseQuery.Where(u =>
                EF.Functions.ILike(u.Email, pattern) ||
                (u.Role != null && EF.Functions.ILike(u.Role, pattern)) ||
                (u.ExternalId != null && EF.Functions.ILike(u.ExternalId, pattern)));
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)size);
        if (totalPages > 0 && safePage > totalPages)
        {
            safePage = totalPages;
        }

        var items = await baseQuery
            .OrderBy(u => u.Email)
            .Skip((safePage - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PageResult<User>(items, totalCount, safePage, size, totalPages);
    }

    /// <inheritdoc />
    public Task<List<User>> SearchByTenantAsync(Guid tenantId, string search, int take = 200, CancellationToken ct = default)
    {
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term)) return Task.FromResult(new List<User>());

        var pattern = $"%{term}%";

        return _db.Users
           .AsNoTracking()
           .Include(u => u.Customer)
           .Where(u => u.TenantId == tenantId)
           .Where(u =>
                EF.Functions.ILike(u.Email, pattern) ||
                (u.Role != null && EF.Functions.ILike(u.Role, pattern)) ||
                (u.ExternalId != null && EF.Functions.ILike(u.ExternalId, pattern)))
           .OrderBy(u => u.Email)
           .Take(take)
           .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<User> CreateAsync(CreateUserCommand command, CancellationToken ct = default)
    {
        var email = command.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            throw new Exception("Email is required.");

        // Validate tenant and customer existence and consistency (customer must belong to tenant).
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == command.CustomerId, ct);
        if (customer is null)
            throw new Exception("Customer not found.");
        if (customer.TenantId != command.TenantId)
            throw new Exception("Customer does not belong to the specified tenant.");

        var role = command.Role?.Trim();
        var externalId = command.ExternalId?.Trim();

        var emailExists = await _db.Users.AnyAsync(u => u.TenantId == command.TenantId && u.Email == email, ct);
        if (emailExists)
            throw new Exception("Email already exists within the tenant.");

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            var externalExists = await _db.Users.AnyAsync(u =>
                u.TenantId == command.TenantId &&
                u.CustomerId == command.CustomerId &&
                u.ExternalId == externalId, ct);

            if (externalExists)
                throw new Exception("ExternalId already exists for this customer.");
        }

        var user = new User
        {
            TenantId = command.TenantId,
            CustomerId = command.CustomerId,
            Email = email,
            Role = string.IsNullOrWhiteSpace(role) ? null : role,
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    /// <inheritdoc />
    public Task<User?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
}
