using System;
using System.Threading;
using System.Threading.Tasks;
using Loyalty.Api.Modules.RulesEngine.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loyalty.Api.Tests;

public class InvoiceProcessingWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_HandlesCancellationDuringDelay()
    {
        var worker = new TestWorker(
            new NoopScopeFactory(),
            new TestOptionsMonitor(new InvoiceProcessorOptions { IntervalSeconds = 5, MaxBatchSize = 0 }),
            new TestLogger());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        await worker.RunAsync(cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_LogsWhenScopeFactoryFails()
    {
        var worker = new TestWorker(
            new ThrowingScopeFactory(),
            new TestOptionsMonitor(new InvoiceProcessorOptions { IntervalSeconds = 5, MaxBatchSize = 1 }),
            new TestLogger());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        await worker.RunAsync(cts.Token);
    }

    private sealed class TestWorker : InvoiceProcessingWorker
    {
        public TestWorker(IServiceScopeFactory scopeFactory, IOptionsMonitor<InvoiceProcessorOptions> options, ILogger<InvoiceProcessingWorker> logger)
            : base(scopeFactory, options, logger)
        {
        }

        public Task RunAsync(CancellationToken token) => ExecuteAsync(token);
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<InvoiceProcessorOptions>
    {
        private readonly InvoiceProcessorOptions _options;

        public TestOptionsMonitor(InvoiceProcessorOptions options) => _options = options;

        public InvoiceProcessorOptions CurrentValue => _options;
        public InvoiceProcessorOptions Get(string? name) => _options;
        public IDisposable? OnChange(Action<InvoiceProcessorOptions, string?> listener) => null;
    }

    private sealed class NoopScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new NoopScope();
    }

    private sealed class NoopScope : IServiceScope
    {
        public IServiceProvider ServiceProvider => new ServiceCollection().BuildServiceProvider();
        public void Dispose() { }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("boom");
    }

    private sealed class TestLogger : ILogger<InvoiceProcessingWorker>
    {
        public IDisposable BeginScope<TState>(TState state) => new NoopDisposable();
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
