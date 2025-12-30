using Loyalty.Api.Data;
using Loyalty.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.GraphQL;

/// <summary>
/// GraphQL read operations for the frontend/admin UI.
/// </summary>
public class Query
{
    /// <summary>
    /// Returns a single customer/outlet with its cached points account balance.
    /// </summary>
    public Task<Customer?> Customer(Guid id, [Service] LoyaltyDbContext db) =>
        db.Customers
          .Include(c => c.PointsAccount)
          .FirstOrDefaultAsync(c => c.Id == id);

    /// <summary>
    /// Lists customers for a given tenant.
    /// Useful for admin UI screens.
    /// </summary>
    public Task<List<Customer>> CustomersByTenant(Guid tenantId, [Service] LoyaltyDbContext db) =>
        db.Customers
          .Where(c => c.TenantId == tenantId)
          .Include(c => c.PointsAccount)
          .OrderBy(c => c.Name)
          .Take(500)
          .ToListAsync();

    /// <summary>
    /// Lists users (employees) for a customer/outlet.
    /// </summary>
    public Task<List<User>> UsersByCustomer(Guid customerId, [Service] LoyaltyDbContext db) =>
        db.Users
          .Where(u => u.CustomerId == customerId)
          .OrderBy(u => u.Email)
          .Take(500)
          .ToListAsync();

    /// <summary>
    /// Returns last N ledger entries for a customer/outlet (immutable points history).
    /// </summary>
    public Task<List<PointsTransaction>> CustomerTransactions(Guid customerId, [Service] LoyaltyDbContext db) =>
        db.PointsTransactions
          .Where(t => t.CustomerId == customerId)
          .OrderByDescending(t => t.CreatedAt)
          .Take(200)
          .ToListAsync();

    /// <summary>
    /// Lists tenants (admin convenience; can be removed once auth/tenant scoping is implemented).
    /// </summary>
    public Task<List<Tenant>> Tenants([Service] LoyaltyDbContext db) =>
        db.Tenants
          .OrderBy(t => t.Name)
          .Take(200)
          .ToListAsync();
}