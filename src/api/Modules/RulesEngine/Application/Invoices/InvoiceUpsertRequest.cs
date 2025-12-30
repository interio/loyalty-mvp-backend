using System.ComponentModel.DataAnnotations;

namespace Loyalty.Api.Modules.RulesEngine.Application.Invoices;

/// <summary>
/// Invoice ingestion request from MuleSoft/ERP. Idempotent per tenant + invoiceId.
/// </summary>
public class InvoiceUpsertRequest
{
    /// <summary>Tenant identifier (explicit mapping from MuleSoft).</summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>Invoice id/number from ERP.</summary>
    [Required]
    public string InvoiceId { get; set; } = default!;

    /// <summary>When the invoice occurred (UTC).</summary>
    [Required]
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>ERP customer id for the outlet.</summary>
    [Required]
    public string CustomerExternalId { get; set; } = default!;

    /// <summary>Currency code (optional).</summary>
    public string? Currency { get; set; }

    /// <summary>Optional actor email attribution.</summary>
    public string? ActorEmail { get; set; }

    /// <summary>Optional actor external id attribution.</summary>
    public string? ActorExternalId { get; set; }

    /// <summary>Invoice lines (cannot be empty).</summary>
    [Required]
    public List<InvoiceLineRequest> Lines { get; set; } = new();
}

/// <summary>Invoice line payload.</summary>
public class InvoiceLineRequest
{
    /// <summary>SKU code.</summary>
    [Required]
    public string Sku { get; set; } = default!;

    /// <summary>Quantity (must be >= 0).</summary>
    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }

    /// <summary>Net amount for the line (must be >= 0).</summary>
    [Range(0, double.MaxValue)]
    public decimal NetAmount { get; set; }
}
