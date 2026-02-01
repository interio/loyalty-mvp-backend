using System.Text.Json;
using System.Text.Json.Nodes;
using Loyalty.Api.Modules.RulesEngine.Application.Invoices;

namespace Loyalty.Api.Modules.RulesEngine.Application.Rules;

/// <summary>Rule that computes points from an invoice.</summary>
public interface IInvoicePointsRule
{
    /// <summary>Rule name for diagnostics.</summary>
    string Name { get; }

    /// <summary>Compute points for the invoice (>=0).</summary>
    int CalculatePoints(InvoiceUpsertRequest invoice);
}

/// <summary>Rule that needs product attribute lookups.</summary>
public interface IInvoicePointsRuleWithProductAttributes : IInvoicePointsRule
{
    void SetProductAttributes(IReadOnlyDictionary<string, JsonObject> attributesBySku);
}

/// <summary>Rule metadata for audit/traceability.</summary>
public record InvoiceRuleMetadata(
    Guid RuleId,
    int RuleVersion,
    string RuleType,
    int Priority,
    bool Active,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    JsonDocument Conditions);

/// <summary>Rule with metadata for audit logging.</summary>
public interface IInvoicePointsRuleWithMetadata : IInvoicePointsRule
{
    InvoiceRuleMetadata Metadata { get; }
}

/// <summary>Rule provider (DB-backed).</summary>
public interface IInvoicePointsRuleProvider
{
    /// <summary>Fetch active rules for a tenant.</summary>
    Task<IReadOnlyList<IInvoicePointsRule>> GetRulesAsync(Guid tenantId, CancellationToken ct = default);
}
