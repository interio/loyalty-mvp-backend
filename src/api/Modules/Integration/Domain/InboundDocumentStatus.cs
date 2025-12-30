namespace Loyalty.Api.Modules.Integration.Domain;

/// <summary>States for inbound document processing.</summary>
public static class InboundDocumentStatus
{
    /// <summary>Document was accepted and stored.</summary>
    public const string Received = "received";

    /// <summary>Document was processed successfully.</summary>
    public const string Processed = "processed";

    /// <summary>Processing failed.</summary>
    public const string Failed = "failed";
}
