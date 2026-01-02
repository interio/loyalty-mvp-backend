using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.Customers.Application;

/// <summary>Command for creating a user under a customer.</summary>
public record CreateUserCommand(Guid TenantId, Guid CustomerId, string Email, string? Role, string? ExternalId);

/// <summary>User application contract within the Customers module.</summary>
public interface IUserService
{
    /// <summary>List users belonging to a customer.</summary>
    Task<List<User>> ListByCustomerAsync(Guid customerId, int take = 500, CancellationToken ct = default);

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
           .Where(u => u.CustomerId == customerId)
           .OrderBy(u => u.Email)
           .Take(take)
           .ToListAsync(ct);

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
