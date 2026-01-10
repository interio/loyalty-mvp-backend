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
using Npgsql;

namespace Loyalty.Api.Modules.RulesEngine.Application;

/// <summary>Handles invoice ingestion and asynchronous points processing.</summary>
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

    /// <summary>Accepts an invoice and stores it for later processing.</summary>
    public async Task<string> IngestInvoiceAsync(InvoiceUpsertRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var correlationId = request.InvoiceId;

        var existing = await _integrationDb.InboundDocuments.FirstOrDefaultAsync(d =>
            d.TenantId == request.TenantId &&
            d.DocumentType == DocumentTypeInvoice &&
            d.ExternalId == request.InvoiceId, ct);

        if (existing is null)
        {
            var node = JsonSerializer.SerializeToNode(request) ?? new JsonObject();
            _integrationDb.InboundDocuments.Add(new InboundDocument
            {
                TenantId = request.TenantId,
                ExternalId = request.InvoiceId,
                DocumentType = DocumentTypeInvoice,
                Payload = (JsonObject)node,
                Status = InboundDocumentStatus.PendingPoints
            });
        }
        else
        {
            // If already stored, ensure it is queued for processing.
            existing.Status = InboundDocumentStatus.PendingPoints;
            existing.Error = null;
            existing.Payload = JsonSerializer.SerializeToNode(request) as JsonObject ?? new JsonObject();
        }

        await _integrationDb.SaveChangesAsync(ct);
        return correlationId;
    }

    /// <summary>Processes up to <paramref name="batchSize"/> pending invoices and awards points.</summary>
    public async Task ProcessPendingInvoicesAsync(int batchSize = 200, CancellationToken ct = default)
    {
        if (batchSize <= 0) return;

        var docs = new List<InboundDocument>();
        if (_integrationDb.Database.IsRelational())
        {
            await using var tx = await _integrationDb.Database.BeginTransactionAsync(ct);
            docs = await _integrationDb.InboundDocuments
                .FromSqlInterpolated($@"
                    SELECT * FROM ""InboundDocuments""
                    WHERE ""Status"" IN ({InboundDocumentStatus.PendingPoints}, {InboundDocumentStatus.Failed})
                      AND ""DocumentType"" = {DocumentTypeInvoice}
                    ORDER BY ""ReceivedAt""
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED")
                .ToListAsync(ct);

            if (docs.Count > 0)
            {
                var ids = docs.Select(d => d.Id).ToList();
                await _integrationDb.InboundDocuments
                    .Where(d => ids.Contains(d.Id))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(d => d.Status, d => InboundDocumentStatus.Processing)
                        .SetProperty(d => d.AttemptCount, d => d.AttemptCount + 1)
                        .SetProperty(d => d.LastAttemptAt, _ => DateTimeOffset.UtcNow)
                        .SetProperty(d => d.Error, _ => null), ct);
            }

            await tx.CommitAsync(ct);
        }
        else
        {
            docs = await _integrationDb.InboundDocuments
                .Where(d => (d.Status == InboundDocumentStatus.PendingPoints || d.Status == InboundDocumentStatus.Failed) &&
                            d.DocumentType == DocumentTypeInvoice)
                .OrderBy(d => d.ReceivedAt)
                .Take(batchSize)
                .ToListAsync(ct);

            foreach (var d in docs)
            {
                d.Status = InboundDocumentStatus.Processing;
                d.AttemptCount += 1;
                d.LastAttemptAt = DateTimeOffset.UtcNow;
                d.Error = null;
            }
            await _integrationDb.SaveChangesAsync(ct);
        }

        if (docs.Count == 0) return;

        foreach (var doc in docs)
        {
            try
            {
                var request = doc.Payload.Deserialize<InvoiceUpsertRequest>(new JsonSerializerOptions()) ??
                              throw new Exception("Stored payload could not be deserialized.");

                var result = await AwardInvoiceAsync(request, ct);
                doc.Status = InboundDocumentStatus.Processed;
                doc.ProcessedAt = DateTimeOffset.UtcNow;
                doc.Error = null;
            }
            catch (Exception ex)
            {
                doc.Status = InboundDocumentStatus.Failed;
                doc.Error = ex.Message;
            }
        }

        await _integrationDb.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Calculates points and posts ledger entry idempotently.
    /// Returns the correlation id (invoiceId), points awarded, duplicate flag, and new balance.
    /// </summary>
    public async Task<InvoiceUpsertResponse> AwardInvoiceAsync(InvoiceUpsertRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var correlationId = request.InvoiceId;

        // Resolve customer by tenant + external id first for correct scoping.
        var customer = await _customersDb.Customers.FirstOrDefaultAsync(c =>
            c.TenantId == request.TenantId && c.ExternalId == request.CustomerExternalId, ct);

        if (customer is null)
            throw new System.Collections.Generic.KeyNotFoundException("Customer not found for provided tenant and external id.");

        // Idempotency: check if this invoice already exists in ledger scoped to tenant/customer.
        var existingTx = await _ledgerDb.PointsTransactions
            .FirstOrDefaultAsync(t =>
                t.CustomerId == customer.Id &&
                t.CorrelationId == correlationId &&
                t.Reason == PointsReasons.InvoiceEarn, ct);

        if (existingTx is not null)
        {
            var existingAccount = await _ledgerDb.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == existingTx.CustomerId, ct);
            return new InvoiceUpsertResponse(correlationId, 0, true, existingAccount?.Balance ?? 0);
        }

        var account = await _ledgerDb.PointsAccounts.FirstOrDefaultAsync(a => a.CustomerId == customer.Id, ct)
                      ?? throw new Exception("Customer has no points account.");

        var (points, appliedRules) = await CalculatePointsAsync(request.TenantId, request, ct);
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
            CreatedAt = DateTimeOffset.UtcNow,
            AppliedRules = appliedRules.Count > 0
                ? JsonSerializer.SerializeToDocument(
                    appliedRules,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                : null
        });

        try
        {
            await _ledgerDb.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _ledgerDb.ChangeTracker.Clear();
            var existingAccount = await _ledgerDb.PointsAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.CustomerId == customer.Id, ct);
            return new InvoiceUpsertResponse(correlationId, 0, true, existingAccount?.Balance ?? 0);
        }

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

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            if (current is PostgresException pg &&
                pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }
            current = current.InnerException;
        }

        return false;
    }

    private async Task<(int total, List<AppliedRuleSnapshot> appliedRules)> CalculatePointsAsync(
        Guid tenantId,
        InvoiceUpsertRequest request,
        CancellationToken ct)
    {
        var total = 0;
        var appliedRules = new List<AppliedRuleSnapshot>();
        var rules = await _rules.GetRulesAsync(tenantId, ct);
        foreach (var rule in rules)
        {
            var points = rule.CalculatePoints(request);
            if (points <= 0) continue;
            total += points;
            if (rule is IInvoicePointsRuleWithMetadata metaRule)
            {
                var conditionsNode = JsonNode.Parse(metaRule.Metadata.Conditions.RootElement.GetRawText()) as JsonObject ?? new JsonObject();
                appliedRules.Add(new AppliedRuleSnapshot(
                    metaRule.Metadata.RuleId,
                    metaRule.Metadata.RuleVersion,
                    metaRule.Metadata.RuleType,
                    metaRule.Metadata.Priority,
                    metaRule.Metadata.Active,
                    metaRule.Metadata.EffectiveFrom,
                    metaRule.Metadata.EffectiveTo,
                    conditionsNode,
                    points));
            }
        }
        return (total, appliedRules);
    }

    private sealed record AppliedRuleSnapshot(
        Guid RuleId,
        int RuleVersion,
        string RuleType,
        int Priority,
        bool Active,
        DateTimeOffset EffectiveFrom,
        DateTimeOffset? EffectiveTo,
        JsonObject Conditions,
        int PointsAwarded);

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
