using System.Text.Json.Nodes;

namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>
/// Raw inbound document from MuleSoft/ERP. Stored as JSONB with minimal metadata for idempotency.
/// </summary>
public class InboundDocument
{
    /// <summary>Primary identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant the document belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>External document id from ERP/MuleSoft.</summary>
    public string ExternalId { get; set; } = default!;

    /// <summary>Document type (e.g., invoice).</summary>
    public string DocumentType { get; set; } = default!;

    /// <summary>Raw payload stored as JSON.</summary>
    public JsonObject Payload { get; set; } = new();

    /// <summary>Optional content hash for dedupe.</summary>
    public string? PayloadHash { get; set; }

    /// <summary>Processing status.</summary>
    public string Status { get; set; } = InboundDocumentStatus.Received;

    /// <summary>Error message if processing failed.</summary>
    public string? Error { get; set; }

    /// <summary>When the document was received.</summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When processing completed.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }
}
