using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.LoyaltyLedger.Application;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.LoyaltyLedger.GraphQL;

/// <summary>Ledger read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class LedgerQueries
{
    /// <summary>Returns recent ledger entries for a customer/outlet.</summary>
    public Task<List<PointsTransactionDto>> CustomerTransactions(Guid customerId, [Service] ILedgerService ledger) =>
        SafeExecute(async () =>
        {
            var rows = await ledger.GetTransactionsForCustomerAsync(customerId);
            return rows.Select(Map).ToList();
        });

    /// <summary>Returns aggregated tenant points metrics for a time window.</summary>
    public Task<TenantPointsSummaryDto> TenantPointsSummary(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        [Service] LedgerDbContext db) =>
        SafeExecute(async () =>
        {
            if (tenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");
            if (to <= from) throw new ArgumentException("to must be greater than from.");

            var tenantCustomerIds = db.Customers
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId)
                .Select(c => c.Id);

            var txQuery = db.PointsTransactions
                .AsNoTracking()
                .Where(t => tenantCustomerIds.Contains(t.CustomerId))
                .Where(t => t.CreatedAt >= from && t.CreatedAt < to);

            var pointsEarned = await txQuery
                .Where(t => t.Reason == PointsReasons.InvoiceEarn && t.Amount > 0)
                .SumAsync(t => (int?)t.Amount) ?? 0;

            var pointsSpent = await txQuery
                .Where(t => t.Reason == PointsReasons.RewardRedeem && t.Amount < 0)
                .SumAsync(t => (int?)(-t.Amount)) ?? 0;

            return new TenantPointsSummaryDto(tenantId, from, to, pointsEarned, pointsSpent);
        });

    private static async Task<T> SafeExecute<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }

    private static PointsTransactionDto Map(PointsTransaction tx) =>
        new(
            tx.Id,
            tx.CustomerId,
            tx.ActorUserId,
            tx.ActorEmail,
            tx.Comment,
            tx.Amount,
            tx.Reason,
            tx.CorrelationId,
            tx.CreatedAt,
            tx.AppliedRules == null ? null : tx.AppliedRules.RootElement.GetRawText());
}

/// <summary>Ledger transaction with optional applied rules snapshot.</summary>
public record PointsTransactionDto(
    Guid Id,
    Guid CustomerId,
    Guid? ActorUserId,
    string? ActorEmail,
    string? Comment,
    int Amount,
    string Reason,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    string? AppliedRulesJson);

/// <summary>Aggregated tenant points metrics for a selected window.</summary>
public record TenantPointsSummaryDto(
    Guid TenantId,
    DateTimeOffset From,
    DateTimeOffset To,
    int PointsEarned,
    int PointsSpent);
