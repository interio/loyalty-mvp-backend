using System.Text.Json;
using HotChocolate;
using HotChocolate.Types;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;
using Loyalty.Api.Modules.RulesEngine.Domain;
using Loyalty.Api.Modules.RulesEngine.Infrastructure.Persistence;
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
        [Service] IntegrationDbContext db) =>
        SafeExecute(async () =>
        {
            var docs = await db.InboundDocuments
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && d.DocumentType == "invoice")
                .OrderByDescending(d => d.ReceivedAt)
                .Take(take <= 0 ? 200 : Math.Min(take, 1000))
                .ToListAsync();

            return docs.Select(FromInboundDocument).ToList();
        });

    private static InboundInvoiceDto FromInboundDocument(InboundDocument doc)
    {
        InvoiceUpsertRequest? parsed = null;
        try
        {
            parsed = doc.Payload.Deserialize<InvoiceUpsertRequest>(new JsonSerializerOptions());
        }
        catch
        {
            parsed = null;
        }

        return new InboundInvoiceDto(
            doc.Id,
            doc.TenantId,
            doc.ExternalId,
            parsed?.CustomerExternalId,
            parsed?.OccurredAt,
            doc.ReceivedAt,
            doc.Status,
            doc.AttemptCount,
            doc.LastAttemptAt,
            doc.ProcessedAt,
            doc.Error);
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
    DateTimeOffset? OccurredAt,
    DateTimeOffset ReceivedAt,
    string Status,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? ProcessedAt,
    string? Error);
