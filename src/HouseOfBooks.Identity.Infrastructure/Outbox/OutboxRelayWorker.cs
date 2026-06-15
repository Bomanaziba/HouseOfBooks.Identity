
namespace HouseOfBooks.Identity.Infrastructure.Outbox;

using System.Text.Json;
using HouseOfBooks.Identity.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Polls Identity.Outbox every N seconds, processes undelivered
/// events, marks them processed. Runs as a hosted background service.
///
/// Replace with MassTransit / NServiceBus outbox if event bus
/// is introduced later — interface stays identical.
/// </summary>
public sealed class OutboxRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayWorker> _logger;

    // Tune via options; 5 s is fine for school ERP workloads
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public OutboxRelayWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRelayWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox relay started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider
                             .GetRequiredService<IOutboxEventProcessor>();
        try
        {
            await processor.ProcessPendingAsync(ct);
        }
        catch (Exception ex)
        {
            // Log and continue — next tick will retry
            _logger.LogError(ex, "Outbox relay batch failed.");
        }
    }
}
