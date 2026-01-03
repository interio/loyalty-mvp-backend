using Microsoft.Extensions.Options;

namespace Loyalty.Api.Modules.RulesEngine.Application;

/// <summary>Background worker that processes pending invoices and awards points.</summary>
public class InvoiceProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<InvoiceProcessorOptions> _options;
    private readonly ILogger<InvoiceProcessingWorker> _logger;

    public InvoiceProcessingWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<InvoiceProcessorOptions> options,
        ILogger<InvoiceProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue ?? new InvoiceProcessorOptions();
            var interval = TimeSpan.FromSeconds(Math.Max(5, opts.IntervalSeconds));

            try
            {
                if (opts.MaxBatchSize > 0)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<PointsPostingService>();
                    await service.ProcessPendingInvoicesAsync(opts.MaxBatchSize, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing pending invoices.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignore cancellation during delay
            }
        }
    }
}
