using System.Text.Json;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.Customers.Application;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Application.Rules;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RulesEngine.Application;

/// <summary>
/// Applies invoices: calculates points using rules, posts ledger entry, and enforces idempotency per invoice.
/// </summary>
public class PointsPostingService
{
    private const string DocumentTypeInvoice = "invoice";
    private readonly LedgerDbContext _ledgerDb;
    private readonly CustomersDbContext _customersDb;
    private readonly IntegrationDbContext _integrationDb;
    private readonly IInvoicePointsRuleProvider _rules;

    /// <summary>Creates the service.</summary>
    public PointsPostingService(LedgerDbContext ledgerDb, CustomersDbContext customersDb, IntegrationDbContext integrationDb, IInvoicePointsRuleProvider rules)
    {
        _ledgerDb = ledgerDb;
        _customersDb = customersDb;
        _integrationDb = integrationDb;
        _rules = rules;
    }

    /// <summary>
    /// Applies an invoice idempotently: calculates points, posts ledger, updates balance.
    /// Returns the correlation id (invoiceId), points awarded, duplicate flag, and new balance.
    /// </summary>
    public async Task<InvoiceUpsertResponse> ApplyInvoiceAsync(InvoiceUpsertRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var correlationId = request.InvoiceId;

        // Idempotency: check if this invoice already exists in ledger.
        var existingTx = await _ledgerDb.PointsTransactions
            .FirstOrDefaultAsync(t => t.CorrelationId == correlationId && t.Reason == PointsReasons.InvoiceEarn, ct);

        if (existingTx is not null)
        {
            var existingAccount = await _ledgerDb.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == existingTx.CustomerId, ct);
            return new InvoiceUpsertResponse(correlationId, 0, true, existingAccount?.Balance ?? 0);
        }

        // Ensure idempotent document storage.
        var docExists = await _integrationDb.InboundDocuments.AnyAsync(d =>
            d.TenantId == request.TenantId &&
            d.DocumentType == DocumentTypeInvoice &&
            d.ExternalId == request.InvoiceId, ct);

        if (!docExists)
        {
            var node = JsonSerializer.SerializeToNode(request) ?? new JsonObject();
            _integrationDb.InboundDocuments.Add(new InboundDocument
            {
                TenantId = request.TenantId,
                ExternalId = request.InvoiceId,
                DocumentType = DocumentTypeInvoice,
                Payload = (JsonObject)node,
                Status = InboundDocumentStatus.Received
            });
            await _integrationDb.SaveChangesAsync(ct);
        }

        // Resolve customer by external id.
        var customer = await _customersDb.Customers.FirstOrDefaultAsync(c =>
            c.TenantId == request.TenantId && c.ExternalId == request.CustomerExternalId, ct);

        if (customer is null)
            throw new System.Collections.Generic.KeyNotFoundException("Customer not found for provided tenant and external id.");

        var account = await _ledgerDb.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == customer.Id, ct)
                      ?? throw new Exception("Customer has no points account.");

        var points = CalculatePoints(request);
        if (points <= 0)
        {
            // No points, but still idempotent response.
            return new InvoiceUpsertResponse(correlationId, 0, false, account.Balance);
        }

        account.Balance += points;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        _ledgerDb.PointsTransactions.Add(new PointsTransaction
        {
            CustomerId = customer.Id,
            ActorUserId = await ResolveActorUserId(request, customer.Id, ct),
            Amount = points,
            Reason = PointsReasons.InvoiceEarn,
            CorrelationId = correlationId,
            CreatedAt = request.OccurredAt
        });

        await _ledgerDb.SaveChangesAsync(ct);

        return new InvoiceUpsertResponse(correlationId, points, false, account.Balance);
    }

    private static void Validate(InvoiceUpsertRequest request)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("tenantId is required.");
        if (string.IsNullOrWhiteSpace(request.InvoiceId)) throw new ArgumentException("invoiceId is required.");
        if (request.OccurredAt == default) throw new ArgumentException("occurredAt is required.");
        if (string.IsNullOrWhiteSpace(request.CustomerExternalId)) throw new ArgumentException("customerExternalId is required.");
        if (request.Lines is null || request.Lines.Count == 0) throw new ArgumentException("lines are required.");

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Sku))
                throw new ArgumentException("line.sku is required.");
            if (line.Quantity < 0)
                throw new ArgumentException("line.quantity must be >= 0.");
            if (line.NetAmount < 0)
                throw new ArgumentException("line.netAmount must be >= 0.");
        }
    }

    private int CalculatePoints(InvoiceUpsertRequest request)
    {
        var total = 0;
        foreach (var rule in _rules.GetRules())
        {
            total += rule.CalculatePoints(request);
        }
        return total;
    }

    private async Task<Guid?> ResolveActorUserId(InvoiceUpsertRequest request, Guid customerId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorExternalId))
        {
            var user = await _customersDb.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.CustomerId == customerId && u.ExternalId == request.ActorExternalId, ct);
            if (user != null) return user.Id;
        }

        if (!string.IsNullOrWhiteSpace(request.ActorEmail))
        {
            var email = request.ActorEmail.Trim().ToLowerInvariant();
            var user = await _customersDb.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.CustomerId == customerId && u.Email == email, ct);
            if (user != null) return user.Id;
        }

        return null;
    }
}
