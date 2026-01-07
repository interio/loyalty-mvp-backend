using System.Text.Json;
using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
using Loyalty.Api.Modules.Customers.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Infrastructure.Persistence;
using Loyalty.Api.Modules.LoyaltyLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Api.Modules.RulesEngine.GraphQL;

/// <summary>Invoice read operations.</summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class InvoiceQueries
{
    /// <summary>Lists recent invoices for a tenant.</summary>
    public Task<List<InboundInvoiceDto>> InvoicesByTenant(
        Guid tenantId,
        int take,
        [Service] IntegrationDbContext db,
        [Service] CustomersDbContext customersDb,
        [Service] LedgerDbContext ledgerDb) =>
        SafeExecute(async () =>
        {
            var docs = await db.InboundDocuments
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && d.DocumentType == "invoice")
                .OrderByDescending(d => d.ReceivedAt)
                .Take(take <= 0 ? 200 : Math.Min(take, 1000))
                .ToListAsync();

            if (docs.Count == 0) return new List<InboundInvoiceDto>();

            var parsedDocs = docs.Select(d =>
            {
                InvoiceUpsertRequest? parsed = null;
                try
                {
                    parsed = d.Payload.Deserialize<InvoiceUpsertRequest>(new JsonSerializerOptions());
                }
                catch
                {
                    parsed = null;
                }
                return (Doc: d, Parsed: parsed);
            }).ToList();

            var invoiceIds = parsedDocs.Select(d => d.Doc.ExternalId).Distinct().ToList();
            var customerExternalIds = parsedDocs
                .Select(d => d.Parsed?.CustomerExternalId)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();

            var customers = await customersDb.Customers
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && customerExternalIds.Contains(c.ExternalId))
                .Select(c => new { c.Id, c.ExternalId })
                .ToListAsync();

            var customerIdByExternal = customers
                .Where(c => c.ExternalId != null)
                .ToDictionary(c => c.ExternalId!, c => c.Id);

            var customerIds = customers.Select(c => c.Id).ToList();
            var transactions = await ledgerDb.PointsTransactions
                .AsNoTracking()
                .Where(t =>
                    t.Reason == PointsReasons.InvoiceEarn &&
                    t.CorrelationId != null &&
                    invoiceIds.Contains(t.CorrelationId) &&
                    customerIds.Contains(t.CustomerId))
                .Select(t => new { t.CustomerId, t.CorrelationId, t.AppliedRules })
                .ToListAsync();

            var appliedRulesByKey = transactions
                .Where(t => t.CorrelationId != null)
                .ToDictionary(
                    t => (t.CustomerId, t.CorrelationId!),
                    t => t.AppliedRules == null ? null : t.AppliedRules.RootElement.GetRawText());

            return parsedDocs
                .Select(d => FromInboundDocument(d.Doc, d.Parsed, customerIdByExternal, appliedRulesByKey))
                .ToList();
        });

    private static InboundInvoiceDto FromInboundDocument(
        InboundDocument doc,
        InvoiceUpsertRequest? parsed,
        Dictionary<string, Guid> customerIdByExternal,
        Dictionary<(Guid CustomerId, string CorrelationId), string?> appliedRulesByKey)
    {
        var lines = parsed?.Lines?
            .Select(l => new InboundInvoiceLineDto(l.Sku, l.Quantity, l.NetAmount))
            .ToList() ?? new List<InboundInvoiceLineDto>();

        var appliedRulesJson = (parsed?.CustomerExternalId != null &&
                                customerIdByExternal.TryGetValue(parsed.CustomerExternalId, out var customerId) &&
                                appliedRulesByKey.TryGetValue((customerId, doc.ExternalId), out var rulesJson))
            ? rulesJson
            : null;

        return new InboundInvoiceDto(
            doc.Id,
            doc.TenantId,
            doc.ExternalId,
            parsed?.CustomerExternalId,
            parsed?.Currency,
            parsed?.ActorEmail,
            parsed?.ActorExternalId,
            parsed?.OccurredAt,
            doc.ReceivedAt,
            doc.Status,
            doc.AttemptCount,
            doc.LastAttemptAt,
            doc.ProcessedAt,
            doc.Error,
            lines,
            appliedRulesJson);
    }

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
}

/// <summary>Invoice summary for admin UI.</summary>
public record InboundInvoiceDto(
    Guid Id,
    Guid TenantId,
    string InvoiceId,
    string? CustomerExternalId,
    string? Currency,
    string? ActorEmail,
    string? ActorExternalId,
    DateTimeOffset? OccurredAt,
    DateTimeOffset ReceivedAt,
    string Status,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? ProcessedAt,
    string? Error,
    List<InboundInvoiceLineDto> Lines,
    string? AppliedRulesJson);

/// <summary>Invoice line summary.</summary>
public record InboundInvoiceLineDto(
    string Sku,
    decimal Quantity,
    decimal NetAmount);
