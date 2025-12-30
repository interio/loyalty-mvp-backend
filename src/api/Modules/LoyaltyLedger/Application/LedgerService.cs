using Loyalty.Api.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.LoyaltyLedger.Application;

/// <summary>Command for redeeming points on a customer account.</summary>
public record RedeemPointsCommand(Guid CustomerId, Guid ActorUserId, int Amount, string Reason, string? CorrelationId);

/// <summary>Ledger application contract.</summary>
public interface ILedgerService
{
    /// <summary>Fetch recent transactions for a customer.</summary>
    Task<List<PointsTransaction>> GetTransactionsForCustomerAsync(Guid customerId, int take = 200, CancellationToken ct = default);

    /// <summary>Redeem points and update cached balance.</summary>
    Task<PointsAccount> RedeemAsync(RedeemPointsCommand command, CancellationToken ct = default);
}

/// <summary>
/// Ledger module application service (immutable transactions + cached balance).
/// </summary>
public class LedgerService : ILedgerService
{
    private readonly LoyaltyDbContext _db;

    /// <summary>Constructs the ledger service.</summary>
    public LedgerService(LoyaltyDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<List<PointsTransaction>> GetTransactionsForCustomerAsync(Guid customerId, int take = 200, CancellationToken ct = default) =>
        _db.PointsTransactions
           .Where(t => t.CustomerId == customerId)
           .OrderByDescending(t => t.CreatedAt)
           .Take(take)
           .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<PointsAccount> RedeemAsync(RedeemPointsCommand command, CancellationToken ct = default)
    {
        if (command.Amount <= 0)
            throw new Exception("Amount must be greater than 0.");

        var reason = command.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new Exception("Reason is required.");

        // Validate actor user belongs to the same customer.
        var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == command.ActorUserId, ct);
        if (actor is null)
            throw new Exception("Actor user not found.");
        if (actor.CustomerId != command.CustomerId)
            throw new Exception("Actor user does not belong to the specified customer.");

        var account = await _db.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == command.CustomerId, ct);
        if (account is null)
            throw new Exception("Customer has no points account.");

        // Optional idempotency.
        var corr = command.CorrelationId?.Trim();
        if (!string.IsNullOrWhiteSpace(corr))
        {
            var exists = await _db.PointsTransactions.AnyAsync(t =>
                t.CustomerId == command.CustomerId && t.CorrelationId == corr, ct);

            if (exists)
                return account;
        }
        else
        {
            corr = null;
        }

        // Debit points as a negative ledger entry.
        var delta = -command.Amount;
        var newBalance = account.Balance + delta;

        if (newBalance < 0)
            throw new Exception("Insufficient points.");

        _db.PointsTransactions.Add(new PointsTransaction
        {
            CustomerId = command.CustomerId,
            ActorUserId = command.ActorUserId,
            Amount = delta,
            Reason = reason,
            CorrelationId = corr
        });

        account.Balance = newBalance;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return account;
    }
}
