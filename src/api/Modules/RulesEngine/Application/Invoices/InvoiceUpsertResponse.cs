namespace Loyalty.Api.Modules.RulesEngine.Application.Invoices;

/// <summary>Response after applying invoice points.</summary>
public record InvoiceUpsertResponse(
    string CorrelationId,
    int PointsAwarded,
    bool WasDuplicate,
    long NewBalance);
