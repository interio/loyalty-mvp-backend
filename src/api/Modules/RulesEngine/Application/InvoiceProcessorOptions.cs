namespace Loyalty.Api.Modules.RulesEngine.Application;

/// <summary>Config for invoice processing worker.</summary>
public class InvoiceProcessorOptions
{
    /// <summary>Interval between polling runs in seconds.</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>Maximum invoices processed per run.</summary>
    public int MaxBatchSize { get; set; } = 200;
}
