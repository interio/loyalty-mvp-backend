namespace Loyalty.Api.Modules.RulesEngine.Domain;

/// <summary>States for inbound document processing.</summary>
public static class InboundDocumentStatus
{
    /// <summary>Document was accepted and stored.</summary>
    public const string Received = "received";

    /// <summary>Document is awaiting points processing.</summary>
    public const string PendingPoints = "pending_points";

    /// <summary>Document is currently being processed by a worker.</summary>
    public const string Processing = "processing";

    /// <summary>Document was processed successfully.</summary>
    public const string Processed = "processed";

    /// <summary>Processing failed.</summary>
    public const string Failed = "failed";
}
